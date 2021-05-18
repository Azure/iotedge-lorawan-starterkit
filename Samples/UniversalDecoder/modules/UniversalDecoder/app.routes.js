'use strict';

const glob = require('glob');
const express = require('express');
const app = express();

app.get('/api/DecoderValueSensor', (req, res) => {
  console.log('Request received');

  var bytes = Buffer.from(req.query.payload, 'base64').toString('utf8');

  res.send({
    value: bytes,
  });
})

app.get('/api/:decodername', (req, res) => {
  console.log(`Request received for ${req.params.decodername}`);

  var files = glob.sync(`./codecs/**/${req.params.decodername}.js`);
  // TODO: error handling if files.length == 0 or files.length > 1

  var bytes = Buffer.from(req.query.payload, 'base64').toString('utf8');
  var fPort = parseInt(req.query.fport);
  // TODO: input validation

  const decoder = require(files[0]);
  // TODO: error handling if module load fails

  const input = {
    bytes,
    fPort
  };

  console.log(`Decoder ${req.params.decodername} input: ${JSON.stringify(input)}`);
  var output = decoder.decodeUplink(input);
  console.log(`Decoder ${req.params.decodername} output: ${JSON.stringify(output)}`);

  res.send({
    value: output.data,
  });
})

module.exports = app;