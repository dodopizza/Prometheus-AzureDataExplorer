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
2) `Viewer` на базу в кусто (на сам ресурс не нужно, достаточно только на базу)
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
		2) Заходим в Azure Data Explorer и создаем таблицу `Metrics` (см.ниже KQL на создание)
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
.create table Metrics (Timestamp: string, Name: string, Instance: string, Job: string, Labels: dynamic, LabelsProm: dynamic, Value: real)
.create-or-alter table Metrics ingestion csv mapping 'CsvMapping' 
'['
'   { "column" : "Timestamp", "DataType":"string", "Properties":{"Ordinal":"0"}},'
'   { "column" : "Name", "DataType":"string", "Properties":{"Ordinal":"1"}},'
'   { "column" : "Instance", "DataType":"string", "Properties":{"Ordinal":"2"}},'
'   { "column" : "Job", "DataType":"string", "Properties":{"Ordinal":"3"}},'
'   { "column" : "Labels", "DataType":"dynamic", "Properties":{"Ordinal":"4"}},'
'   { "column" : "LabelsProm", "DataType":"dynamic", "Properties":{"Ordinal":"5"}},'
'   { "column" : "Value", "DataType":"real", "Properties":{"Ordinal":"6"}},'
']'
```


## Get metrics example
```
Metrics
| where tolong(Timestamp) between (1587000000000 .. 1587376860000) and ( Name == "node_disk_io_now" )
| order by Timestamp asc
| extend timeval = pack( "Timestamp", Timestamp, "Value", Value )
| summarize Samples=make_list(timeval) by tostring(Labels)
| extend timeseries=pack( "Samples", Samples, "Labels", parse_json(Labels) )
| project timeseries
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