#!/bin/bash

TEST_RESULTS_PATH="./TestResults"

function fail {
  echo $1 >&2
  exit 1
}

trap 'fail "The test execution was aborted because a command exited with an error status code."' ERR


function retry {
  local n=1
  local max=3
  local delay=5
  while true; do
    "$@" && break || {
      if [[ $n -lt $max ]]; then
        ((n++))
        echo "[WARN] Test failed. Attempt $n/$max:"
        sleep $delay;
      else
        fail "[FAIL] The test has failed after $n attempts."
      fi
    }
  done
}



if [ "$1" != "" ]; then
    TEST_RESULTS_PATH="$1"
fi

# Starts with tests the can fail quick (pktforwarder is down, or something in this scenario)
declare -a testsToRun=('OTAAJoinTest'
                       'ABPTest' 
                       'C2DMessageTest' 
                       'OTAATest'
                       'SensorDecodingTest')

testCount=${#testsToRun[@]}

for (( i=1; i<${testCount}+1; i++ ));
do
  echo "[INFO] Starting ${testsToRun[$i-1]} ($i/${testCount})..."
  retry dotnet test --filter ${testsToRun[$i-1]} --logger trx --results-directory $TEST_RESULTS_PATH
done

echo "[INFO] Done executing tests!"