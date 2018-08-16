#This docker builds a container for the AAEON intel LoRaWaN gateway
FROM microsoft/dotnet:2.0-runtime AS builder
RUN apt-get update
RUN apt-get install -y git
RUN apt-get install -y --no-install-recommends apt-utils build-essential
RUN git clone https://github.com/Lora-net/packet_forwarder.git
RUN git clone https://github.com/Lora-net/lora_gateway.git
RUN sed -i "s|/dev/spidev0.0|/dev/spidev2.0|g" ./lora_gateway/libloragw/src/loragw_spi.native.c
RUN sed -i "s|IOT_SK_SX1301_RESET_PIN=7|IOT_SK_SX1301_RESET_PIN=5|g" ./lora_gateway/reset_lgw.sh
WORKDIR /packet_forwarder
RUN ./compile.sh
WORKDIR /build

FROM microsoft/dotnet:2.0-runtime AS exec
WORKDIR /LoRa
COPY --from=builder /packet_forwarder/lora_pkt_fwd/lora_pkt_fwd .
COPY --from=builder /packet_forwarder/lora_pkt_fwd/global_conf.json .
COPY --from=builder /lora_gateway/reset_lgw.sh .
COPY local_conf.json .
COPY start_pktfwd.sh .
ENTRYPOINT ["./start_pktfwd.sh","5"]
