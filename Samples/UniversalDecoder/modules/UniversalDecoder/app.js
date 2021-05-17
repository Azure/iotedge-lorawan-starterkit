'use strict';

const express = require('express')
const app = express()
const port = 8080

app.get('/', (req, res) => {
  console.log('Request received');

  var bytes = Buffer.from(req.query.payload, 'base64').toString('utf-8');
  var fPort = req.query.fport;

  res.send({
    fPort,
    bytes
  });
})

app.listen(port, () => {
  console.log(`Example app listening at http://localhost:${port}`)
})
