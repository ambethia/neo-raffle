# neo-raffle

A smart contract that enables a raffle/lottery on the Neo block chain. Surprisingly tricky.

**Send GAS, win GAS!**

The contract on TestNet is: [AGbcQUTrFbK7xSJeUZgFHLVAxXE9R3RiKf](https://neoscan-testnet.io/address/AGbcQUTrFbK7xSJeUZgFHLVAxXE9R3RiKf)

Here's an example round:

## Tickets

Ticket #1: [e09083d2ac634092d2fc31ca56f8750fbd70749ba7944358f1054e3a040ebb6f](https://neoscan-testnet.io/transaction/e09083d2ac634092d2fc31ca56f8750fbd70749ba7944358f1054e3a040ebb6f) 2.5 GAS

Ticker #2: [03dfeb213d99593a3c9666520c9e28cc24133224c969e7bb03504b0a248e93a6](https://neoscan-testnet.io/transaction/03dfeb213d99593a3c9666520c9e28cc24133224c969e7bb03504b0a248e93a6) 3 GAS 

Ticket #3: [4f918745534f3aac4af071fa0957028cfa53d3ca2cc3a85af5982ee0d7372007](https://neoscan-testnet.io/transaction/4f918745534f3aac4af071fa0957028cfa53d3ca2cc3a85af5982ee0d7372007) 1 GAS

## The Drawing

The drawing happens in two steps, first a [sweep transaction](https://neoscan-testnet.io/transaction/542d6fd57c69704d44ee76b57525e92333e65f4b8a7a35430bf3054351bd3f9a) sweeps all the tickets into one transaction that gets sent back the contract address. This TXID is used as a parameter to [invoke the contract's "drawing" operation](ac48ee7ce1604d4213a8b6734e2867785fc7c1c2840fd49949b75c3580461ad6), where the winner is picked from the sweep's inputs and saved in storage.


## The Payout!

Winner was [AYnmjohQjxiV9TRVjKTYZW9dth9YZJvy5B ](https://neoscan-testnet.io/address/AYnmjohQjxiV9TRVjKTYZW9dth9YZJvy5B) with [5.5 GAS](https://neoscan-testnet.io/transaction/f9af7e9ce9a5ddf90f506b9fee1a45696b6aa282bd4782f1d59c4f0f5c248e97) (6.5 less the rake, capped at 1 GAS).
