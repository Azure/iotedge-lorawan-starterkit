'use strict';

const express = require('express');
const app = express();

app.get('/api/DecoderValueSensor', (req, res) => {
  console.log('Request received');

  var bytes = Buffer.from(req.query.payload, 'base64').toString('utf8');

  res.send({
    value: bytes,
  });
})

app.get('/api/loravisionshield', (req, res) => {
  console.log('Request received');

  const decoder = require('./codecs/arduino/loravisionshield');
  var bytes = Buffer.from(req.query.payload, 'base64').toString('utf8');
  var result = decoder.decodeUplink({bytes});
  res.send({
    value: result.data,
  });
})

module.exports = app;