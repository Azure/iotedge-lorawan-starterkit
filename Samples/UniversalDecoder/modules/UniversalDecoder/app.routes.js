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

app.get('/api/:decodername', async (req, res) => {
  console.log(`Request received for ${req.params.decodername}`);

  console.log("Looking for " + `./codecs/**/${req.params.decodername}.js`);
  await glob(`./codecs/**/${req.params.decodername}.js`, {}, (err, files)=>{
    console.log(err);
    console.log(files);
  });

  const decoder = require(`./codecs/arduino/${req.params.decodername}`);
  var bytes = Buffer.from(req.query.payload, 'base64').toString('utf8');
  var result = decoder.decodeUplink({bytes});
  res.send({
    value: result.data,
  });
})


module.exports = app;