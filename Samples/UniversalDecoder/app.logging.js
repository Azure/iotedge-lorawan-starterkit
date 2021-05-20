'use strict'

const pino = require('pino');
const expressPino = require('express-pino-logger');

const logger = pino({ level: process.env.LOG_LEVEL || 'info' });
const expressLogger = expressPino({ 
    logger,
    serializers: {
        req: (req) => ({
            method: req.method,
            url: req.url,
        }),
        res: (res) => ({
            statusCode: res.statusCode,
        }),
        err: (err) => ({
            type: err.type,
            message: err.message
        }),
    },
});

module.exports = {logger, expressLogger};