'use strict';

const glob = require('glob');
const fs = require('fs');

glob.sync(`./codecs/**/*.js`).map(f => {
    fs.appendFile(f, '\nmodule.exports={decodeUplink};', function (err) {
        if (err) throw err;
        console.log(`Modified ${f}`);
      });
});

glob.sync(`./codecs/**/*.jpg`).map(f => {
  fs.rm(f, function (err) {
      if (err) throw err;
      console.log(`Removed ${f}`);
    });
});

glob.sync(`./codecs/**/*.png`).map(f => {
  fs.rm(f, function (err) {
      if (err) throw err;
      console.log(`Removed ${f}`);
    });
});