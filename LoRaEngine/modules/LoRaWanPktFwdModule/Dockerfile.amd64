#This docker builds a container for the AAEON intel LoRaWaN gateway

FROM microsoft/dotnet:2.0-runtime AS builder1
RUN apt-get update
RUN apt-get install -y git
RUN apt-get install -y --no-install-recommends apt-utils build-essential
RUN git clone https://github.com/Lora-net/packet_forwarder.git
RUN git clone https://github.com/Lora-net/lora_gateway.git
RUN sed -i "s|/dev/spidev0.0|/dev/spidev1.0|g" ./lora_gateway/libloragw/src/loragw_spi.native.c
WORKDIR /packet_forwarder
RUN ./compile.sh
RUN cp /packet_forwarder/lora_pkt_fwd/lora_pkt_fwd /lora_pkt_fwd_spidev1

FROM microsoft/dotnet:2.0-runtime AS builder2
RUN apt-get update
RUN apt-get install -y git
RUN apt-get install -y --no-install-recommends apt-utils build-essential
RUN git clone https://github.com/Lora-net/packet_forwarder.git
RUN git clone https://github.com/Lora-net/lora_gateway.git
RUN sed -i "s|/dev/spidev0.0|/dev/spidev2.0|g" ./lora_gateway/libloragw/src/loragw_spi.native.c
WORKDIR /packet_forwarder
RUN ./compile.sh
RUN cp /packet_forwarder/lora_pkt_fwd/lora_pkt_fwd /lora_pkt_fwd_spidev2

FROM debian:stretch-slim AS exec
WORKDIR /LoRa
COPY --from=builder1 /lora_pkt_fwd_spidev1 .
COPY --from=builder2 /lora_pkt_fwd_spidev2 .
COPY --from=builder1 /packet_forwarder/lora_pkt_fwd/global_conf.json .
COPY --from=builder1 /packet_forwarder/lora_pkt_fwd/global_conf.json global_conf.eu.json
COPY --from=builder1 /lora_gateway/reset_lgw.sh .
COPY global_conf.us.json .
COPY local_conf.json .
COPY start_pktfwd.sh .
ENTRYPOINT ["./start_pktfwd.sh"]