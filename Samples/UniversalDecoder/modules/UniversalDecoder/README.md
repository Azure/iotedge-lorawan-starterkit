# Universal Decoder

- The structure of the lorawan-devices provided by TTN is described [here](https://github.com/TheThingsNetwork/lorawan-devices#files-and-directories)
- Initially we decided not to do any yaml parsing and work with the assumption that the codec implementation files are named after the codec they are implementing
- The `/vendor` folder will be pulled at docker build time and copied into the container
  - This allows to import js files at runtime
  - This allows to implement a quality gate before releasing a new version of the docker container (prevent bugs coming from external repo)
  - This allows for rollback to an earlier working version of the docker image
- Docker image build should be automated and scheduled according to the requirements

## Setup

- Add configuration to your .env:
  - CONTAINER_REGISTRY=xxx
  - CONTAINER_REGISTRY_USERNAME=xxx
  - CONTAINER_REGISTRY_PASSWORD=xxx
- npm install
- npm run codecs
- Azure Iot: Set default target platform for Edge Solution --> select appropriately

## Run application

- npm start

## Run test

- npm test

## TODOs

- Remove pictures from codecs