## Ресурсы
Имя ресурса | Тип | Инфо
---|---|---
`cluster`prometehusadx | Storage  Account |
`cluster`-prometheus-adx | Service Principal |
`cluster`-prometheus-adx | Event Hub Namespace
`cluster`-prometheus-adx | Event Hub
`cluster`-prometheus-adx | Event Grid Subscription
`dododevkusto` | Azure Data Explorer Cluster | Относится к **dev/ld**
`dodologs` | Azure Data Explorer Cluster | Относится к **production**


1) Выдать `Contributor` на функцию
2) `Admin` на базу в кусто (на сам ресурс не нужно, достаточно только на базу)
3) `Event Hub`:
	1) Создаем Event Hub (Namespace) - `cluster`-prometheus-adx
	2) В Event Hub Namespace создаем Event Hub - `cluster`-prometheus-adx
4) `Event Grid Subscriptions`
	1) Создаем Event Subscription `cluster`-prometheus-adx:
		* Topic type - Storage Accounts
		* Topic resource - наш `cluster`prometehusadx (Storage  Account)
		* Filter event type - blob created
		* Endpoint details - наш `cluster`-prometheus-adx (Event Hub)
5) Идем в нужный `Azure Data Explorer Cluster`
	1) Создаем базу `cluster`-prometheus
		1) Permissions: в базу добавляем нашего `cluster`-prometheus-adx (Service Principal) с ролью View
		2) Заходим в Azure Data Explorer и создаем таблицы `RawData`, `Metrics` (см.ниже KQL на создание)
		2) Data  injestion:
			* Connection type - Blob storage
			* Storage Account / Event Grid - наши созданные выше ресурсы
			* Table - `Metrics`
			* Data format - CSV
			* Column Mapping - CsvMapping



## Presetup
```
dotnet restore
```

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
