# The specifications moved

The TOTP token format specification and its HTTP authentication binding are
maintained in **[OpenChargingTechnology/Whitepapers →
TimeBasedOneTimePasswords](https://github.com/OpenChargingTechnology/Whitepapers/tree/master/TimeBasedOneTimePasswords)**
— their normative home — together with the machine-readable test-vector
annex and its generator:

 * [totp-token-format.md](https://github.com/OpenChargingTechnology/Whitepapers/blob/master/TimeBasedOneTimePasswords/totp-token-format.md)
   (v1.0, final)
 * [totp-http-authentication.md](https://github.com/OpenChargingTechnology/Whitepapers/blob/master/TimeBasedOneTimePasswords/totp-http-authentication.md)
   (v1.0, draft)
 * [test-vectors/](https://github.com/OpenChargingTechnology/Whitepapers/tree/master/TimeBasedOneTimePasswords/test-vectors)
   — the normative annex

This repository consumes them as the `libs/specification` submodule and
remains the cross-implementation conformance suite executing the annex.
