'use strict';

const express = require('express');
const app = express();

app.get('/api/:decoder', (req, res) => {
  console.log('Request received');

  var bytes = Buffer.from(req.query.payload, 'base64').toString('utf8');

  res.send({
    value: bytes,
  });
})

module.exports = app;