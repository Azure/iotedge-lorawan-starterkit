'use strict';

const port = 8080;

const app = require('./app.routes');
app.listen(port, () => {
  console.log(`Server started at http://localhost:${port}`)
})
