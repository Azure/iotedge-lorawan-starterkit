#!/bin/bash
set -e
export CERT_WORKDIR=$(cd $(dirname $0) && pwd)
. "$CERT_WORKDIR/helper-functions"

case "$1" in
  server|s)
    if [ -z "$2" ];
    then
        echo "You need to specify common name for server certificate.";
    else
        echo "Generating server certificate";
        if [ ! -d "$CERT_WORKDIR/ca" ];
        then
            echo "No root certificate previously generated, generating one now...";
            rootCA root $CERT_WORKDIR/ca
        fi
        servercert $2 $CERT_WORKDIR/ca/root $CERT_WORKDIR/server pfx $3
    fi
    ;;
  client|c)
    if [ -z "$2" ];
    then
        echo "You need to specify common name for client certificate.";
    else
        echo "Generating client certificate";
        if [ ! -d "$CERT_WORKDIR/ca" ];
        then
            echo "No root certificate previously generated, generating one now...";
            rootCA root $CERT_WORKDIR/ca
        fi
        clientcert $2 $CERT_WORKDIR/ca/root $CERT_WORKDIR/client
    fi
    ;;
  *)
    usage
    ;;
esac