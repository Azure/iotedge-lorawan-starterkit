'use strict';

const glob = require('glob');
const express = require('express');
const { param, query, validationResult } = require('express-validator');
const { expressLogger, logger } = require('./app.logging');

const app = express();

app.use(expressLogger);

app.get('/api/:decodername', 
    param('decodername').notEmpty().withMessage("is missing"),
    query('payload').notEmpty().withMessage("is missing").isBase64().withMessage("must be a base64 encoded string"),
    query('fport').isInt(),
    (req, res, next) => {
        // Input validation
        if (!validationResult(req).isEmpty()) {
            const error = `Invalid inputs: ${(validationResult(req).formatWith(e => `'${e.param}' ${e.msg}`).array().join(", "))}`;
            logger.warn(error);
            return res.status(400).send({error});
        }
      
      const decoderName = req.params.decodername;
      const bytes = Buffer.from(req.query.payload, 'base64').toString('utf8').split('');
      const fPort = parseInt(req.query.fport);
    
      const decoder = getDecoder(decoderName);
    
      const input = {
        bytes,
        fPort
      };
    
      logger.debug(`Decoder ${decoderName} input: ${JSON.stringify(input)}`);
      var output = decoder.decodeUplink(input);
      logger.debug(`Decoder ${decoderName} output: ${JSON.stringify(output)}`);
    
      res.send({
        value: output.data,
      });
});

// gets decoder by name
function getDecoder(decoderName) {
  if (decoderName === 'DecoderValueSensor') {
    // return inline decoder implementation
    return {
      decodeUplink: (input) => { return { data: input.bytes.join('') } }
    }
  }
  
  // search for codec in "codecs" directory
  var files = glob.sync(`./codecs/**/${decoderName}.js`);
  if (files.length === 0) {
    throw new Error(`No codec found: ${decoderName}`);
  } else if (files.length > 1) {
    throw new Error(`Multiple codecs found: ${JSON.stringify(files)}`);
  }
  
  return require(files[0]);
}

// Error handling
app.use(function (err, req, res, next) {
  logger.error(err);
  res.status(500).send({
    error: err.message,
    rawPayload: req.query.payload
  });
})

module.exports = app;