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

The test container will automatically close when all tests have been completed.  There is also the option to run the test container using a file watch to aide local development.

```
docker-compose -f docker-compose.test.yaml -f docker-compose.test.watch.yaml build
docker-compose -f docker-compose.test.yaml -f docker-compose.test.watch.yaml up
```
