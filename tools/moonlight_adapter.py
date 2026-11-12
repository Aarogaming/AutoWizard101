"""Run Moonlight's CLI with its legacy Scapy iterator call adapted locally."""

import runpy


def adapt_reader(reader_type):
    """Moonlight 0.4.1 calls .next(); Scapy 2.8 exposes __next__ only."""
    if not hasattr(reader_type, "next"):
        reader_type.next = reader_type.__next__


def main():
    from scapy.utils import PcapReader
    adapt_reader(PcapReader)
    runpy.run_module("moonlight", run_name="__main__")


if __name__ == "__main__":
    main()
