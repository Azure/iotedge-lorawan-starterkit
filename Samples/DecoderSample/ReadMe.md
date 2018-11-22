To add a new decoder, simply copy one of the sample methods from *LoraDecoders.cs*. The payload sent to the decoder is passed as byte[] payload and the Fport as uint fport.
After decoding the message, return a string containing the response to be sent upstream.

Call the decoder by:
http://containername/api/decodername?fport=X&payload=XXXXXXXXX if it is running on the same machine running LoRaWAN IoT Edge engine.
http://machinename:port/api/decodername?fport=X&payload=XXXXXXXXX if it is running on a seperate machine.

To be able to call container webserver only from other containers port 80:
{
	"ExposedPorts": {
		"80/tcp": {}
	}
}

OLD: To be able to call container webserver on machine port 8881:
{
	"ExposedPorts": {
		"80/tcp": {}
	},
	"HostConfig": {
		"PortBindings": {
			"80/tcp": [{
				"HostPort": "8881"
			}]
		}
	}
}