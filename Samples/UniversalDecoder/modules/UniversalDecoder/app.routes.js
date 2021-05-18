'use strict';

const glob = require('glob');
const express = require('express');
const app = express();

app.get('/api/:decodername', (req, res) => {
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

function getDecoder(decoderName) {
  if (decoderName == 'DecoderValueSensor') {
    return {
      decodeUplink: (input) => { return { data: input.bytes.join('') } }
    }
  }
  
  var files = glob.sync(`./codecs/**/${decoderName}.js`);
  // TODO: error handling if files.length == 0 or files.length > 1
  const decoder = require(files[0]);
  // TODO: error handling if module load fails
  return decoder;
}

module.exports = app;