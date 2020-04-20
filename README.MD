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