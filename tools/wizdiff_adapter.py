"""Compare two local patch XML file lists using wizdiff's real parser; no network."""

import argparse
from pathlib import Path

from wizard101_hub import compare_records, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("before")
    parser.add_argument("after")
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    from wizdiff.dml_parser import parse_records_from_xml

    def records(path):
        tables = parse_records_from_xml(Path(path).read_bytes())
        return [record for rows in tables.values() for record in rows if "SrcFileName" in record]

    write_json(args.output, compare_records(records(args.before), records(args.after), "SrcFileName"))


if __name__ == "__main__":
    main()
