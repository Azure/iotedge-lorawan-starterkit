# Adaptive Data Rate

The solution supports Adaptive Data Rate (ADR) device management as specified in
[LoRa spec v1.1][lora-v1.1]. The main goal of the ADR is to optimize the network
for maximum capacity ensuring devices always transmit with their best settings
possible (highest data rate, lowest power), you can find more ADR information on
this [page][lora-adr].

Adaptive Data rate is always initiated and set on the device side. ADR should
never be used with moving devices. Our solution currently implements the
[Semtech proposed Algorithm for ADR][semtech-adr-algorithm] and has been tested
against EU868 region plan. In this algorithm the data rate and transmission
power calculation is done as follows:

1. Signal-to-noise ratio (SNR) is calculated based on the maximum SNR detected
   over the recent transmissions, the required SNR for the given region (which
   depends on the spreading factor of the current data rate) and SNR margin
   (always equal to 5dB). The calculated SNR determines the number of steps that
   will be executed for the calculation of new data rate and transmission power.

1. In each step we try to increment the data rate, as long as it's lower than
   the maximum data rate supported for the region (DR5 in case of EU868). We
   then increment the TX power index as long as it's lower than the highest TX
   power index for the region.

   >To determine the maximum TX power index we use the TX power table which is
   documented in [LoRaWAN Regional Parameters specification][lora-rp]. The table
   defines the mapping of TX power indices (0 - 7 in case of EU868) to EIRP
   (Equivalent Isotropically Radiated Power) in dB. The highest EIRP is mapped
   to the lowest TX power index, hence in order to achieve a lower transmission
   power we increment the TX power index in each step of the algorithm.

1. Once the remaining steps are equal to 0, the new data rate and TX power index
   calculated this way are returned.

1. In the case where the number of steps calculated in the beginning is negative
   the algorithm decrements the TX power index (as long as it's greater than 0)
   but it does not try to lower the data rate, because the end-devices implement
   automatic data rate decay. The algorithm can only actively increase the data
   rate.

[lora-v1.1]: https://lora-alliance.org/resource_hub/lorawan-specification-v1-1/
[lora-adr]: https://www.sghoslya.com/p/how-does-lorawan-nodes-changes-their.html
[semtech-adr-algorithm]: https://www.thethingsnetwork.org/forum/uploads/default/original/2X/7/7480e044aa93a54a910dab8ef0adfb5f515d14a1.pdf
[lora-rp]: https://lora-alliance.org/wp-content/uploads/2021/05/RP002-1.0.3-FINAL-1.pdf
