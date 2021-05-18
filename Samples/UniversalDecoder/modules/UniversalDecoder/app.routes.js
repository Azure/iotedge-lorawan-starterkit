'use strict';

const glob = require('glob');
const express = require('express');
const app = express();

app.get('/api/:decodername', (req, res, next) => {
  console.log(`Request received for ${req.params.decodername}`);

  // TODO: input validation
  const decoderName = req.params.decodername;
  const bytes = Buffer.from(req.query.payload, 'base64').toString('utf8').split('');
  const fPort = parseInt(req.query.fport);

  const decoder = getDecoder(decoderName);

  const input = {
    bytes,
    fPort
  };

  console.log(`Decoder ${decoderName} input: ${JSON.stringify(input)}`);
  var output = decoder.decodeUplink(input);
  console.log(`Decoder ${decoderName} output: ${JSON.stringify(output)}`);

  res.send({
    value: output.data,
  });
})

// gets decoder by name
function getDecoder(decoderName) {
  if (decoderName == 'DecoderValueSensor') {
    // return inline decoder implementation
    return {
      decodeUplink: (input) => { return { data: input.bytes.join('') } }
    }
  }
  
  // search for codec in "codecs" directory
  var files = glob.sync(`./codecs/**/${decoderName}.js`);
  if (files.length == 0) {
    throw new Error(`No codec found: ${decoderName}`);
  } else if (files.length > 1) {
    throw new Error(`Multiple codecs found: ${JSON.stringify(files)}`);
  }
  
  return require(files[0]);
}

// Error handling
app.use(function (err, req, res, next) {
  res.status(500).send({
    error: err.message,
    rawPayload: req.query.payload
  });
})

module.exports = app;