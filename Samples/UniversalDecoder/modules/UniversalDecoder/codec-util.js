'use strict';

const glob = require('glob');
const fs = require('fs');

glob.sync(`./codecs/**/*.js`).map(f => {
    fs.appendFile(f, '\nmodule.exports={decodeUplink};', function (err) {
        if (err) throw err;
        console.log(`Modified ${f}`);
      });
});