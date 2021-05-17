'use strict';

const express = require('express')
const app = express()
const port = 8080

app.get('/', (req, res) => {
  console.log('Request received');
  res.send({
    value: req.query.payload
  });
})

app.listen(port, () => {
  console.log(`Example app listening at http://localhost:${port}`)
})
