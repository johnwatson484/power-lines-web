[![Build Status](https://dev.azure.com/johnwatson484/John%20D%20Watson/_apis/build/status/johnwatson484.power-lines-web?branchName=master)](https://dev.azure.com/johnwatson484/John%20D%20Watson/_build/latest?definitionId=35&branchName=master)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=johnwatson484_power-lines-web&metric=alert_status)](https://sonarcloud.io/dashboard?id=johnwatson484_power-lines-web)

# Power Lines Web
Web front end

# Prerequisites
- Docker
- Docker Compose

# Running the application
The application is intended to be run and developed within a container.  A set of docker-compose files exist to support this.

## Run production application in container

```
docker-compose -f docker-compose.yaml build
docker-compose -f docker-compose.yaml up
```

## Develop application in container

The service is dependent on a message broker. For development a RabbitMQ container is provided.

```
docker network create power-lines
docker-compose build
docker-compose up
```

## Debug application in container
Running the above development container configuration will include a remote debugger that can be connected to using the example VS Code debug configuration within `./vscode`.

The `${command:pickRemoteProcess}` will prompt for which process to connect to within the container.  

Local changes to code files will automatically trigger a rebuild and restart of the application within the container.

## Run tests
Unit tests are written in NUnit.

```
docker-compose -f docker-compose.test.yaml build
docker-compose -f docker-compose.test.yaml up
```
