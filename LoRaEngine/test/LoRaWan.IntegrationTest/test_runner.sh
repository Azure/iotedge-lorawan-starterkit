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
declare -a testsToRun=('LoRaWan.IntegrationTest.OTAAJoinTest'
                       'LoRaWan.IntegrationTest.ABPTest'
                       'LoRaWan.IntegrationTest.C2DMessageTest'
                       'LoRaWan.IntegrationTest.OTAATest'
                       'LoRaWan.IntegrationTest.MacTest'
                       'LoRaWan.IntegrationTest.SensorDecodingTest'
                       'LoRaWan.IntegrationTest.ClassCTest'
                       'LoRaWan.IntegrationTest.DeduplicationTest')

testCount=${#testsToRun[@]}

for (( i=1; i<${testCount}+1; i++ ));
do
  echo "[INFO] Starting ${testsToRun[$i-1]} ($i/${testCount})..."
  if [ "$i" -eq 1 ];then
      retry dotnet test --filter ${testsToRun[$i-1]} --logger trx --results-directory $TEST_RESULTS_PATH
  else
      retry dotnet test --no-build --filter ${testsToRun[$i-1]} --logger trx --results-directory $TEST_RESULTS_PATH
  fi
done

echo "[INFO] Done executing tests!"