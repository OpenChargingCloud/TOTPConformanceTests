# OCPP 2.1 Edition 2, Part 2 — TOTP-related sections (verbatim extract)

> **Source:** *OCPP 2.1 — Part 2 - Specification*, Edition 2, 2025-12-03,
> by the [Open Charge Alliance](https://openchargealliance.org)
> (`OCPP-2.1_edition2_part2_specification.pdf`).
>
> **License of the extracted text:** the source document states (Disclaimer, p. 1):
> *"Copyright © 2010 – 2025 Open Charge Alliance. All rights reserved. This
> document is made available under the \*Creative Commons
> Attribution-NoDerivatives 4.0 International Public License\*
> (https://creativecommons.org/licenses/by-nd/4.0/legalcode)."*
> This file reproduces excerpts of that document under [CC BY-ND
> 4.0](https://creativecommons.org/licenses/by-nd/4.0/). The wording is
> **unmodified** (including original typos, marked `[sic]`); only the layout
> is converted from PDF to Markdown — a format shift, not an adaptation — and
> omissions (figures, unrelated sections) are marked. The `[sic]` marks,
> bracketed notes and the final "Related" section are annotations of this
> repository, not part of the source. This one file is therefore licensed CC
> BY-ND 4.0 © Open Charge Alliance, **not** under the repository's Apache-2.0
> license.
>
> Extracted 2026-08-22 for conformance comparison within TOTPConformanceTests
> (see `docs/ocpp-totp-comparison.md`). Page numbers are the document's own
> (`x/803`); the PDF file pages are 141–142, 158–166 and 750–751.

---

## C. Authorization — introduction, "Ad hoc via QR code" (pp. 128–129)

> Charging Station that do not have an integrated or stand-alone payment
> terminal can still support ad hoc payment via a payment website. A common
> approach for this is to show a QR code on the Charging Station that provides
> the URL of the payment website, which the user can access using a mobile
> device. Once payment details are entered and validated, CSMS remotely starts
> a charging session for the user. In order to prevent fraud by pasting fake QR
> stickers on the Charging Station it is recommended to show a dynamic QR code
> on the Charging Station display.
>
> This is described in use case C25 - Ad hoc payment via a QR code.

---

## C25 - Ad hoc payment via a QR code (pp. 145–147)

*New in OCPP 2.1*

| No. | Type | Description |
|----|------|-------------|
| 1 | Name | Ad hoc payment via a QR code |
| 2 | ID | C25 |
| 3 | Objective | To provide a static or dynamic QR code with a URL for ad hoc payment |
| 4 | Description | Static QR code: The Charging Station has a static QR code (often a sticker) to scan to start a transaction.<br>Dynamic QR code: The Charging Station displays a dynamic QR code with a time-based one-time password that is based on a shared secret between Charging Station and CSMS.<br>The EV Driver scans the QR code to access a website where the EV Driver can enter payment credentials for the charging service. In case of a dynamic QR code the CSMS checks that the URL scanned from the QR code is authentic. Upon successful payment authorization CSMS issues a remote start of a transaction on the Charging Station (and optional EVSE) that is encoded in the URL. |
| | Actors | EV Driver, PSP, CSMS, Charging Station |

**Scenario description #1 — Scanning a dynamic QR code**

1. Charging Station displays a QR code with a time-based one-time password.
   a. Optionally, Charging Station requests input from EV Driver to specify a maximum time, energy or cost prior to displaying the QR code
   b. The input is added as query parameters to the URL in the QR code.
2. EV Driver scans QR code with URL to access a web page of CSMS to initiate a charging session.
3. CSMS decodes URL according to the configured URL template and validates the time-based one-time password.
   a. The URL contains as a minimum the Charging Station identity, one time password and version, and optionally the EVSE number, but other parameters can be configured in the URL template as well.
   b. If URL validation or decoding fails no charging session will be started. Use case ends here.
4. CSMS optionally sends a NotifyWebPaymentStartedRequest message with evseId and timeout to notify Charging Station of the event.
   a. Charging Station displays feedback to EV Driver and prevents that a transaction is started locally on the EVSE in evseId for as long as timeout seconds, or until the RequestStartTransactionRequest from CSMS is received.
5. CSMS diverts EV Driver to website of PSP (Payment Service Provider).
6. EV Driver enters payment credentials in web page shown by PSP.
7. Upon approval PSP returns an approved status and a unique reference for this payment in the PSP platform.
   a. This unique reference is called "pspRef" in OCPP.
   b. PSP sends payment details, such as PspRef, cardBin, cardLast4Digits, etc. to CSMS.
8. CSMS sends a RequestStartTransactionRequest to Charging Station and EVSE from the URL and with idToken having value \<pspRef\> and type = DirectPayment.
9. Use case continues as per use case "F07 Remote start with cost limit" in which CSMS will provide a transactionLimit.maxCost set to the payment card authorization amount, which acts as a ceiling for the cost, independent of a value entered bij `[sic]` EV Driver in step 1.

**Scenario description #2 — Scanning a static QR code**

> **WARNING** A static QR code is susceptible to fraud. A new QR sticker that points to a fake payment site can be pasted over the original QR sticker.

1. Charging Station displays a static QR code (often a sticker).
2. EV Driver scans QR code with URL to access a web page of CSMS to initiate a charging session.
3. CSMS decodes URL.
   a. The URL contains the Charging Station identity and optional EVSE number.
   b. (The URL format is known by CSMS and does not need to be configured in the Charging Station.)
   c. If URL validation or decoding fails no charging session will be started. Use case ends here.
4. The remaining steps 4 to 9 are identical to scenario description #1 above.

**Scenario description #3 — Settlement when transaction has finished**

1. If cost calculated locally
   a. Charging Station sends a TransactionEventRequest with eventType = Ended and costDetails to CSMS.
   b. CSMS responds with TransactionEventResponse.
   c. Charging Station shows the cost breakdown in costDetails to EV Driver.
2. If cost calculated centrally
   a. Charging Station sends a TransactionEventRequest with eventType = Ended to CSMS.
   b. CSMS responds with TransactionEventResponse with totalCost and an updatedPersonalMessage.
   c. Charging Station shows the cost breakdown in updatedPersonalMessage to EV Driver.
3. CSMS performs settlement directly with PSP based on the \<pspRef\>.
4. PSP returns a receipt data or a URL to a receipt to CSMS.
5. CSMS may use SetDisplayMessageRequest to display a URL to the receipt to the EV Driver.

| No. | Type | Description |
|----|------|-------------|
| 5 | Prerequisites | Dynamic QR code: The Charging Station has been configured with dynamic QR code configuration variables in WebPaymentsCtrlr, and Charging Station periodically displays a QR code with a new time-based one-time password. |
| 6 | Postcondition(s) | CSMS remotely starts a transaction for EV Driver. |
| 7 | Error handling | If the time-based one-time password verfication `[sic]` fails, then CSMS will not start a transaction. |
| 8 | Remark(s) | The communication between CSMS and PSP, as well as the manner how a receipt is communicated to the EV Driver, is out of scope of this document.<br>This use case resembles use case C24 "Ad hoc payment via stand-alone payment terminal" with the difference that the credit/debit card terminal is replaced by the web page that the URL in the QR code points to. |

> **NOTE** The mechanism to provide a URL for web payment via a QR code, as
> described in this use case, also applies to other mechanisms that can provide
> a URL, such as NFC tags or Bluetooth Beacons.

> This use case assumes that the CSMS of the CSO handles URL validation and
> communication with PSP, as is also the case in the other ad hoc payment use
> cases (directly or indirectly via a payment terminal).
>
> It is also possible to involve an EMSP in the use case to handle payment. In
> that case the URL points to the EMSP system that executes steps 2 to 7 of the
> use case after which it requests CSMS (via a roaming protocol) to start a
> session on the charging station.

*(Figure 46 "Ad hoc payment via QR code", a sequence diagram, omitted. Its
example URL parameter line reads: `Visit payment web page with URL
(chargingstationid = CS-001, totp = aHicux89, version = v1, maxcost = 12.34)`.)*

---

## URL definition for dynamic QR code (pp. 148–149)

> The QR code contains the URL to the ad hoc payment web page for the CSMS.
> This web page can be provided by a third party, like a PSP, but it is
> responsibility of the CSO. The format of the URL is up to the CSO, as long as
> certain mandatory URL parameters exist that are needed to support the use
> case.
>
> The URL is a defined `[sic]` as a template that contains parts ("variables")
> that are filled in by the Charging Station. These variables are put between
> curly braces as path parameters in the URL template. For example:

Example URL without EVSE ID

```
https://qr.example.com/{totp}/{version}/{chargingstationid}/
```

Example URL with EVSE

```
https://qr.example.com/{chargingstationid}/{evse}/{totp}/{version}
```

> The URL template is defined in WebPaymentsCtrlr.URLTemplate.

**URL pointing to CSMS**

> In the situation as described in the use case the URL will point to the CSMS.
> CSMS will use a PSP to handle the payment, but CSMS is responsible for
> coordination and initiating the transaction. In the URL template the Charging
> Station will replace the variables:
>
> * chargingstationid with its Charging Station identity,
> * evse with the evse for which the payment is requested.
> * totp with the calculated time-based one-time password,
> * version with the version of the time-based one-time password algorithm,
>
> A URL may contain additional query parameters that are filled in by the
> Charging Station based upon user input. This would allow an EV driver to
> specify, for example, a maximum amount of energy to charge. The following
> variables are defined, and when supported by the Charging Station, are
> reported in the variable named WebPaymentsCtrlr.URLParameters.
>
> * maxenergy for maximum energy (in Wh) to be charged
> * maxtime for maximum charging time (in seconds)
> * maxcost for the maximum cost (in currency of charging station)

Example URL for EVSE #1 of CS1000 with maximum energy of 20 kWh

```
https://qr.example.com/CS1000/1/aHicux89/v1/?maxenergy=20000
```

> The only purpose of this is to communicate a maximum energy/time/cost request
> to CSMS before the authorization takes place. Once the transaction is
> started, this data will be communicated to CSMS as part of the
> transactionLimit field in the TransactionEventRequest.

**URL pointing to external party**

> In the case that the URL points to an external party, such as an EMSP, the
> external party is responsible for coordination and initiating the start of a
> transaction. The external party will decode the URL.
>
> Instead of a Charging Station identity, this URL will have to contain the
> roaming EVSE ID for the EVSE of the Charging Station. CSMS has to set the
> variable RoamingEvseId in WebPaymentsCtrlr to the roaming EVSE ID.
>
> In the URL template the Charging Station will replace the variables:
>
> * roamingevseid with the roaming EVSE id,
> * totp with the calculated time-based one-time password,
> * version with the version of the time-based one-time password algorithm,

Example URL template using a roaming ID of charging station

```
https://qr.example.com/{roamingevseid}/{totp}/{version}
```

> This requires a WebPaymentsCtrlr at the EVSE tier of the device model, i.e.
> each EVSE has its own WebPaymentsCtrlr such that CSMS can configure the
> roaming EVSE ID in WebPaymentCtrlr.RoamingEvseId `[sic]` for each EVSE.
>
> The roaming EVSE ID needs to have been agreed upon by and communicated to the
> external party. A common standard is the format of eMI3/ISO15118
> [ref-ISOIEC15118-2].

---

## C25 - Ad hoc payment via static or dynamic QR code - Requirements (pp. 149–151)

> **NOTE** For a static QR code any requirements related to a one-time password (TOTP) are not applicable.

**URL validation for dynamic QR code**

| ID | Precondition | Requirements | Note |
|----|--------------|--------------|------|
| C25.FR.01 | When component WebPaymentsCtrlr exists and has "Enabled" = true | Charging Station SHALL display a dynamic QR code according to configuration variables in WebPaymentsCtrlr. | See URL definition for dynamic QR code for description of URL template. |
| C25.FR.03 | If Charging Station supports WebPaymentsCtrlr.URLParameters | Charging Station SHALL allow input from EV Driver for these parameters (e.g. maximum time, energy or cost) to be added as query parameters to the URL in the QR code. | This requires that the users inputs `[sic]` values before the QR code is displayed. |
| C25.FR.04 | C25.FR.03 AND URL parameter "maxtime" is supported. EV Driver specified a maximum time | CS SHALL append the query variable "maxtime" with the time in seconds to the URL and enforce the maximum transaction duration. | This also means that CS will add a transactionLimit.maxTime for this value to the TransactionEventRequest messages. (See E16). |
| C25.FR.05 | C25.FR.03 AND URL parameter "maxenergy" is supported. EV Driver specified a maximum energy to charge | CS SHALL append the query variable "maxenergy" with the energy in Wh and enforce the maximum charging energy. | This also means that CS will add a transactionLimit.maxEnergy for this value to the TransactionEventRequest messages. (See E16). |
| C25.FR.06 | C25.FR.03 AND URL parameter "maxcost" is supported. EV Driver specified a maximum cost for charging | CS SHALL append the query variable "maxcost" to the URL and enforce the maximum charging cost. | This also means that CS will add a transactionLimit.maxCost for this value to the TransactionEventRequest messages. (See E16). |
| C25.FR.07 | When EV Driver visits the web page of CSMS using the URL in the QR code | CSMS SHALL validate the time-based one-time password in the URL | See description in Validation of TOTP. |
| C25.FR.08 | If the time-based one-time password in the URL is not valid | CSMS SHALL NOT forward EV Driver to a web page of PSP to enter payment credentials and SHALL NOT authorize EV Driver to charge | No payment is authorized and no transaction is started. |
| C25.FR.09 | If the time-based one-time password in the URL is valid | CSMS SHALL decode the URL parameters as defined in URL definition for dynamic QR code. | URL parameters must at least include an identity of the Charging Station and optionally an EVSE number. |

**Payment authorization**

| ID | Precondition | Requirements | Note |
|----|--------------|--------------|------|
| C25.FR.20 | If URL successfully decoded | CSMS SHALL forward EV Driver to a web page of PSP to enter payment credentials | The protocol between PSP and CSMS is not specified in this document. |
| C25.FR.21 | C25.FR.20 | CSMS MAY send a NotifyWebPaymentStartedRequest message with evseId having the EVSE number that was decoded from the URL and timeout the time in seconds that Charging Station may lock the EVSE to prevent locally started transactions. | After timeout seconds Charging Station should free the EVSE again for local transactions. |
| C25.FR.22 | C25.FR.21 | Charging Station SHOULD provide feedback to EV Driver that the QR code was successfully scanned and SHOULD prevent further local interactions with the EVSE referred to in evseId for as long as timeout seconds or until a RequestStartTransactionRequest is received from CSMS. | |
| C25.FR.23 | When CSMS receives a message from Payment Kiosk that payment is authorized with authorization reference \<PspRef\> for Charging Station with identity \<CS\> and optionally an EVSE \<evse\> | CSMS SHALL send a RequestStartTransactionRequest to Charging Station with identity \<CS\> and EVSE \<evse\> (when present) and having idToken.idToken = \<PspRef\> and idToken.type = DirectPayment | There is need to include additionalInfo elements for payment details, like cardBin, cardLast4Digits, towards Charging Station.<br>The protocol between Payment Kiosk and CSMS is not specified in this document. |
| C25.FR.24 | C25.FR.23 AND Charging Station sends a TransactionEventRequest with eventType = Started | CSMS SHALL respond with a TransactionEventResponse with transactionLimit set to the required limit. | transactionLimit uses maxCost for cost limit, maxEnergy for energy limit, maxTime for time limit or maxSoC for SoC limit. |
| C25.FR.25 | C25.FR.24 | Charging Station SHALL include this limit in the field transactionInfo.transactionLimit (once) in the next TransactionEventRequest | |
| C25.FR.26 | | Charging Station SHALL behave as in use case E16 - Transactions with fixed cost, energy, SoC or time. | |
| C25.FR.27 | Upon receiving a NotifyWebPaymentStartedRequest | Charging Station SHALL respond with a NotifyWebPaymentStartedResponse without any parameters. | |

**Settlement**

| ID | Precondition | Requirements | Note |
|----|--------------|--------------|------|
| C25.FR.40 | After CSMS received a TransactionEventRequest with eventType = Ended or the transaction | CSMS SHALL send a message to PSP to settle the cost | Cost may have been calculated locally or centrally. |
| C25.FR.41 | When CSMS receives receipt information or a URL to a receipt from PSP | CSMS MAY send a SetDisplayMessageRequest with a URL where EV Driver can download the receipt. | Other methods to find and download receipts may also be offered by CSO. |

**URL requirements for dynamic QR code**

| ID | Precondition | Requirements | Note |
|----|--------------|--------------|------|
| C25.FR.50 | | The URL template for the dynamic QR code SHALL contain the following placeholder for URL parameters:<br>• {chargingstationid} or {roamingevseid}<br>• {totp} for the generated TOTP<br>• {version} for the TOTPVersion | |
| C25.FR.51 | If the WebPaymentsCtrlr.URLTemplate contains {chargingstationid} | Charging Station SHALL replace placeholder {chargingstationid} in the URL template by the value of SecurityCtrlr.Identity | |
| C25.FR.52 | If the WebPaymentsCtrlr.URLTemplate contains {evse} | Charging Station SHALL replace placeholder {evse} in the URL template by the number of the EVSE for which the payment request is done | |
| C25.FR.53 | If the WebPaymentsCtrlr.URLTemplate contains {roamingevseid} | Charging Station SHALL replace placeholder {roamingevseid} in the URL template by the value of WebPaymentsCtrlr.RoamingEvseId | |
| C25.FR.56 | If Charging Station wishes to communicate a maximum energy amount for the QR code authorization | Charging Station SHALL add the query parameter "maxenergy=XXX" to the URL, where XXX is the maximum energy (in Wh) to be charged. | |
| C25.FR.57 | If Charging Station wishes to communicate a maximum charging time for the QR code authorization | Charging Station SHALL add the query parameter "maxtime=XXX" to the URL, where XXX is the maximum charging time (in seconds). | |
| C25.FR.58 | If Charging Station wishes to communicate a maximum cost for the QR code authorization | Charging Station SHALL add the query parameter "maxcost=XXX" to the URL, where XXX is the maximum cost (in currency of tariff at charging station). | |
| C25.FR.59 | | CSMS SHALL accept the query parameters maxenergy, maxtime and maxcost in a QR code authorization URL. | |
| C25.FR.60 | | CSMS MAY ignore a non-supported query parameter in the QR code authorization URL. | If a maximum limit was provided, Charging Station will apply this to the transaction, regardless whether CSMS has support for it. |

---

## Time-based One-Time password algorithm (pp. 152–153)

> The algorithm to calculate the time-based one-time password uses the
> HMAC-SHA256 hash function on the byte representation of the start of the
> current time interval in seconds. The bytes are mapped to digits and
> characters. For added security the mapping starts not at the first byte, but
> at a semi-random offset that is calculated from the last byte of the hash.
>
> The configuration parameters for the time-based one-time password are set in
> the WebPaymentsCtrlr: ValidityTime, SharedSecret and Length.

### TOTP algorithm, version 1

> The algorithm is defined in pseudocode as follows:

TOTP v1 algorithm in pseudocode

```
// Input parameters
set validityTime to <parameter ValidityTime>
set totpLength to <parameter Length>
set sharedSecret to <parameter SharedSecret>
set base62 to
"0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"

// Get time interval
calculate timeInSeconds as current UTC time - Unix Epoch (01-01-1970)
calculate timeInterval as floor of timeInSeconds / validityTime
convert timeInterval to bytes and store in timeBytes
if system's byte order is Little Endian then
    reverse byte order of timeBytes

// Calculate hash
create new HMAC-SHA256 hash function with sharedSecret as the key (converted
to bytes using UTF-8 encoding)
calculate hash of timeBytes using HMAC-SHA256 and store in hash variable

// Convert hash bytes to characters starting at random offset
calculate offset as (last byte of hash) bitwise AND 0x0F
create a new empty string builder
for i from 0 to totpLength do
    find the character at position (hash value at index (offset + i) modulo
length of hash)
        modulo length of base62 in the base62 string
    append this character to the string builder
end for
return string builder as string
```

> The following is a C# version of this algorithm:

TOTP v1 algorithm in C#

```csharp
// Input parameters
var validityTime    = TimeSpan.FromSeconds(ValidityTime); // parameter from URL
var totpLength      = Length;       // parameter from URL
var sharedSecret    = SharedSecret; // parameter from URL
var base62          =
"0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

// Get time interval
var timeInSeconds   = DateTime.UtcNow - new DateTime(1970, 1, 1);
var timeInterval    = (UInt64) (timeInSeconds.TotalSeconds /
validityTime.TotalSeconds);
var timeBytes       = BitConverter.GetBytes(timeInterval);
if (BitConverter.IsLittleEndian)
    Array.Reverse(timeBytes);

using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sharedSecret)))
{
    // Calculate hash
    var hash = hmac.ComputeHash(timeBytes);

    // Convert hash bytes to characters starting at random offset
    var offset         = hash[^1] & 0x0F;
    var stringBuilder  = new StringBuilder();

    for (var i = 0; i < totpLength; i++)
        stringBuilder.Append(base62[hash[(offset + i) % hash.Length] %
base62.Length]);

    return stringBuilder.ToString();
}
```

### Validation of TOTP

> CSMS validates the time-based one-time password (TOTP) in the URL against the
> TOTP that CSMS calculates for the current time. The TOTP in the URL
> (URL-TOTP) is considered valid if it equals the TOTP calculated by CSMS
> (CSMS-TOTP). If the TOTPs are not equal CSMS shall try to compare the
> URL-TOTP against a CSMS-TOTP for the previous time interval and the next time
> interval. This overcomes failed validation as a result `[sic]` processing
> delays or small differences in clock time.
>
> If URL-TOTP and CSMS-TOTP still do not match in any of these cases, then CSMS
> shall consider the URL as invalid.

---

## 2.4.2. WebPaymentsCtrlr (pp. 737–738, "Referenced Components and Variables")

> This logical component is used to configure the creation of dynamic QR codes
> for payment. See use case C25 - Ad hoc payment via static or dynamic QR code

| Variable | Type | RW | M/O | Description |
|----------|------|----|-----|-------------|
| URLTemplate | string | RW | M | URL template |
| URLParameters | MemberList | RO | O | List of supported URL query parameters "maxtime", "maxenergy" and "maxcost".<br>valuesList: "maxtime", "maxenergy", "maxcost".<br>When absent, none of these are supported. |
| TOTPVersion | OptionList | RW | M | Version of the TOTP algorithm to use.<br>valuesList: "v1"<br>The "TOTP algorithm, version 1" from C25 - Ad hoc payment via a QR code is referenced as "v1".<br>When a Charging Station supports multiple versions, they are listed in valuesList. |
| RoamingEvseId | string | RW | O | Roaming EVSE Id to be used when URL is pointing to an external party. |
| ValidityTime | integer | RW | M | Validity of a one-time password in seconds.<br>Acceptable range between 6 seconds up to 3600 seconds. |
| SharedSecret | string | WO | M | \<random text\> set to a random value on first boot.<br>Must be at least 8 characters long. |
| Length | integer | RW | M | Length of TOTP, e.g. 8 characters.<br>Must be at least 6 characters. |
| QRCodeQuality | OptionList | RO/RW | O | The error correction level, defined as: Low, Medium, Quartile, High |

*M = mandatory, O = optional; RO = read-only, RW = read-write, WO = write-only*

---

## Related, but not TOTP-specific (references only)

 * **NotifyWebPaymentStarted** request/response (Part 2, section 1.58, p. 604):
   CSMS → Charging Station notification that a web payment process (e.g. QR
   code scanning) has started; carries `evseId` and `timeout`.
 * **DisplayMessage message format `QRCODE`** (Appendix, p. 700): "Message
   content is a text (usually a URL) that Charging Station will display as a QR
   code on the display. Note: this is not a dynamic QR code and should not be
   used for payments."
 * **Component list** (p. 716): "WebPaymentsCtrlr — Responsible for
   configuration of a dynamic QR code for ad hoc" payment.
 * Use case **C24 - Ad hoc payment via stand-alone payment terminal** (pp.
   142–145): the payment-terminal sibling of C25, no TOTP involvement.
