'use strict';

const app = require('./app.routes');
const port = 8080;

app.listen(port, () => {
  console.log(`Server started at http://localhost:${port}`)
})
