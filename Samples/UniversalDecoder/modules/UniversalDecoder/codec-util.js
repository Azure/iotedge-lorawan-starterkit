'use strict';

const glob = require('glob');
const fs = require('fs');
const fse = require('fs-extra');

var args = process.argv.slice(2);
const srcDir = args[0] || './node_modules/lorawan-devices/vendor';
const dstDir = args[1] || './codecs';

glob.sync(`**/*`, 
  {
    cwd: srcDir,
    nodir: true,
    ignore: [
      '**/*.jpg',
      '**/*.png',
    ]
  }).map(f => {
    const srcPath = `${srcDir}/${f}`;
    const dstPath = `${dstDir}/${f}`;

    console.log(`Copying ${srcPath} to ${dstPath}`);
    fse.copySync(srcPath, dstPath);

    if (f.endsWith(".js")) {
      fs.appendFile(dstPath, '\nmodule.exports={decodeUplink};', function (err) {
        if (err) throw err;
        console.log(`Patching ${dstPath}`);
      });
    }
});
