#! /bin/bash
./vsts-agent/bin/Agent.Listener configure --unattended --url "https://$VSTS_ACCOUNT.visualstudio.com" --auth PAT --token $VSTS_TOKEN --pool $VSTS_POOL --agent $VSTS_AGENT --replace --acceptTeeEula
./vsts-agent/bin/Agent.Listener run