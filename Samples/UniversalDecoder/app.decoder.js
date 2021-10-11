'use strict';

const glob = require('glob');
const path = require('path');
const {logger} = require('./app.logging');

function getAllDecoders() {
    return glob.sync(`./codecs/**/*.js`).map(d => path.basename(d).split('.')[0]);
}

// gets decoder by name
function getDecoder(decoderName) {
    if (decoderName === 'DecoderValueSensor') {
        // return inline decoder implementation
        return {
            decodeUplink: (input) => { return { data: input.bytes.join('') } }
        }
    }

    // search for codec in "codecs" directory
    const files = glob.sync(`./codecs/**/${decoderName}.js`);
    if (files.length === 0) {
        throw new Error(`No codec found: ${decoderName}`);
    } else if (files.length > 1) {
        throw new Error(`Multiple codecs found: ${JSON.stringify(files)}`);
    }

    return require(files[0]);
}

function decode(decoderName, payload, fPort) {
    const decoder = getDecoder(decoderName);

    const input = {
        bytes: Buffer.from(payload, 'base64').toString('utf8').split(''),
        fPort: parseInt(fPort)
    };

    logger.debug(`Decoder ${decoderName} input: ${JSON.stringify(input)}`);
    const output = decoder.decodeUplink(input);
    logger.debug(`Decoder ${decoderName} output: ${JSON.stringify(output)}`);
    
    return output;
}

module.exports = { getAllDecoders, decode };
