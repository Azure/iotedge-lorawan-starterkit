'use strict';

const express = require('express');
const {param, query, validationResult} = require('express-validator');
const {expressLogger, logger} = require('./app.logging');
const decoder = require('./app.decoder');

const app = express();

app.use(expressLogger);

app.get('/decoders', (req, res, next) => {
    res.send(decoder.getAllDecoders());
});

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

        const output = decoder.decode(req.params.decodername, req.query.payload, req.query.fport);

        res.send({
            value: output.data,
        });
    });

// Error handling
app.use(function (err, req, res, next) {
    logger.error(err);
    res.status(500).send({
        error: err.message,
        rawPayload: req.query.payload
    });
})

module.exports = app;
