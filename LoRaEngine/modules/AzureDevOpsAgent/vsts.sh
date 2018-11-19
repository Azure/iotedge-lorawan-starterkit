#! /bin/bash
./vsts-agent/bin/Agent.Listener configure --unattended --url $VSTS_SERVER_URL --auth PAT --token $VSTS_TOKEN --pool default --agent myAgent --replace --acceptTeeEula
./vsts-agent/bin/Agent.Listener run