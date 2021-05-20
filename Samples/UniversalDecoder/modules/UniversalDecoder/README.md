# Universal Decoder

This project gives access to decoders in the [TTN repo](https://github.com/TheThingsNetwork/lorawan-devices#payload-codecs) through a HTTP REST interface compliant with the LoraWan implementation in this repository. 

Codecs provided by TTN are stored in well defined [folder structure](https://github.com/TheThingsNetwork/lorawan-devices#files-and-directories). The universal decoder copies the codec files into its docker image at build time for later use from the web application. As currently codecs are not implemented as node modules (see [open issue](https://github.com/TheThingsNetwork/lorawan-devices/issues/177)), these files need to be patched accordingly so that they can be imported and reused.

## Quick start

Install node dependencies and copy codecs from the TTN repository:
```
npm install
npm run codecs
```

Create docker image
```
docker build . -f Dockerfile.amd64 -t universaldecoder
```

Run docker image
```
docker run -d -p 8080:8080 universaldecoder
```

Call universal decoder at
```
http://localhost:8080/api/DecoderValueSensor?devEui=0000000000000000&fport=1&payload=QUJDREUxMjM0NQ%3D%3D
```

## Local development

### Start local server

```
npm start
```

You can access the universal decoder at the url available in the output of the previous command.

### Run tests

```
npm test
```

## Deploying to Azure IoT Edge

- Add configuration to your `.env` file:
```
CONTAINER_REGISTRY_USERNAME_xxx=xxx
CONTAINER_REGISTRY_PASSWORD_xxx=xxx
```

- Azure Iot: Set default target platform for Edge Solution --> select appropriately

## TODOs

- Documentation
  - Logging with pino
 - Parse yaml structure to find codecs 
  