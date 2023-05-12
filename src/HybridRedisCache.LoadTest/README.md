# HybridRedisCache.LoadTest


## How to install Grafana/K6 on Ubuntu
> sudo gpg -k
> sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
> echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
> sudo apt-get update
> sudo apt-get install k6

## How to install Grafana/K6 on windows
> winget install k6

## How to install Grafana/K6 on Docker
> docker pull grafana/k6:latest

Run K6:
> docker run --rm -i grafana/k6 run - <appjs

For more information about installing: https://k6.io/docs/get-started/installation/
