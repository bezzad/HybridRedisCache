# HybridRedisCache.LoadTest

The is the load test project which is use Grafana/k6 to test sample WebAPI and produce stress and load test report.
To execute this test you must install Grafana/k6. To visualizing real-time SRC data of load test you need to install Grafana and Prometheus.

## How to install dependencies

1. Run Redis on docker
> docker run --name redis -p 6379:6379 -d redis:latest

2. Run WebAPI as Release mode

> cd "..\HybirdRedisCache.Sample.WebAPI" 

> dotnet run --Configuration Release

3. Run Prometheus with modified configs to read API metrics

> docker run -d --name prometheus --network monitoring -p 9090:9090 prom/prometheus

open docker and go to the view files of prom/prometheus container
go to file tab and edit below file:

> "etc/prometheus/prometheus.yml"

Append below code to end of prometheus.yml file:

``` yml
  - job_name: "weather_api"
    static_configs:
      - targets: ['host.docker.internal:5000']
    metrics_path: /metrics
```

Save and restart Prometheus service

4. Run Grafana and add prometheus to that

> docker run -d --name=grafana --network monitoring -p 3000:3000 grafana/grafana

* Open `Firefax` or `Eadge` browser and go to http://localhost:3000   (`Chrome` not support unsecure URLs)
* Login with default **user**/**pass**:  `admin`/`admin`
* Add a Data Source > Prometheus Type
* Just add prometheus address in docker:

> http://host.docker.internal:9090

* Click on Save & Test

5. Creat Grafana Dashboard for Prometheus incoming data
Go to below address in browser:

> http://localhost:3000/dashboard/import

import `GrafanaDashboard.json` from current path.

6. Run Grafana/k6 to start load test 

## How to install Grafana/K6 on windows
> winget install k6

or 

> choco install k6

## How to install Grafana/K6 on Docker
> docker pull grafana/k6:latest

Run K6:
> docker run --rm -i grafana/k6 run - <app.js

For more information about installing: https://k6.io/docs/get-started/installation/


## How to run load test
Open CMD and start `start.cmd` file in root folder of load test.

## Load test result with +2000 virtual users with multiple request per user
![Redis vs. InMemory](https://raw.githubusercontent.com/bezzad/HybridRedisCache/main/img/LoadTestResult.png)
