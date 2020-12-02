## Resources

Resource | Resource Type
---|---
`<cluster>-prometheus-adx` | Service Principal
`<cluster>-prometheus-adx` | Function App
`<cluster>-adx` | Azure Data Explorer Cluster
`<cluster>prometheusadx`  | Storage  Account
`<cluster>-prometheus-adx` | Event Hub Namespace
`<cluster>-prometheus-adx` | Event Hub Instance
`<cluster>-prometheus-adx` | Event Grid Subscription

## Step-by-step guide

1) Create `Service Principal` account;
2) Create `Storage Account`;
3) Create `Azure Data Explorer Cluster`;
4) Create `Function App`;
5) Add `Contributor` for `Service Principal` account;
6) Add `Admin` for `Azure Data Explorer Cluster` database (only for database);
7) Create `Event Hub Namespace` with `Event Hub` Instance
8) Create `Event Grid Subscription` with following options:
	* Topic type - **Storage Accounts**
	* Topic resource - `Storage  Account` (2)
	* Filter event type - **Blob created**
	* Endpoint details - `Event Hub` (7)
9) Create database `<cluster>-prometheus` in `Azure Data Explorer Cluster`:
	* Permissions: Add `View` role for `Service Principal` (1)
	* Create tables `RawData`, `Metrics` (KQL bellow)
	* Data injestion:
		* Connection type - **Blob storage**
		* Storage Account / Event Grid - `Storage Account` (2) and `Event Grid Subscription` (8)
		* Table - **Metrics**
		* Data format - **CSV**
		* Column Mapping - **CsvMapping**

## From prometheus to Kusto injestion flow
1) Prometheus > remote_write > azure blob storage (csv)
2) EventGrid

## Create table
```
.drop table Metrics ifexists

.create table RawData (Datetime: datetime, Timestamp: long, Name: string, Instance: string, Job: string, Labels: dynamic, LabelsHash: long, Value: real)

.create-or-alter table RawData ingestion csv mapping 'CsvMapping'
'['
'   { "column" : "Datetime", "DataType":"datetime", "Properties":{"Ordinal":"0"}},'
'   { "column" : "Timestamp", "DataType":"long", "Properties":{"Ordinal":"1"}},'
'   { "column" : "Name", "DataType":"string", "Properties":{"Ordinal":"2"}},'
'   { "column" : "Instance", "DataType":"string", "Properties":{"Ordinal":"3"}},'
'   { "column" : "Job", "DataType":"string", "Properties":{"Ordinal":"4"}},'
'   { "column" : "Labels", "DataType":"dynamic", "Properties":{"Ordinal":"5"}},'
'   { "column" : "LabelsHash", "DataType":"long", "Properties":{"Ordinal":"6"}},'
'   { "column" : "Value", "DataType":"real", "Properties":{"Ordinal":"7"}},'
']'

.alter-merge table RawData policy retention softdelete = 4d recoverability = disabled

.create table Metrics (LabelsHash: long, StartDatetime: datetime, EndDatetime: datetime, Name: string, Instance: string, Job: string, Labels: dynamic, Samples: dynamic)
```


## Get metrics example
```
Metrics
| where (EndDatetime >= unixtime_milliseconds_todatetime(1591084670098)) and (StartDatetime <= unixtime_milliseconds_todatetime(1591092170098)) and ( ( Name == 'mysql_global_status_queries' ) )
| summarize Labels=tostring(any(Labels)), Samples=make_list( Samples ) by LabelsHash
| mv-apply Samples = Samples on
(
    order by tolong(Samples['Timestamp']) asc
    | summarize Samples=make_list(pack('Timestamp', Samples['Timestamp'], 'Value', Samples['Value']))
)
```

## Prometheus read/write format
```
Samples:[
	{
		timestamp:
		value:
	},
	{
		timestamp:
		value:
	}...
],
Labels:[
    {
		name:
		value:
	},
	{
		name:
		value:
	}...
]
```
