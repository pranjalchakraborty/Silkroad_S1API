import json
import os
import argparse
import glob
import re
import sys
from pathlib import Path

# --- Configuration ---
DEFAULT_EMPIRE_FILE = "empire.json"
# Use a suffix for split files to make combining easier
SPLIT_FILE_SUFFIX = "_dealer.json"

# --- Helper Functions ---

def sanitize_filename(name):
    """Removes or replaces characters invalid for filenames."""
    # Remove characters that are definitely invalid on most systems
    name = re.sub(r'[\\/*?:"<>|]', "", name)
    # Replace spaces with underscores for better compatibility
    name = name.replace(" ", "_")
    # Ensure the name is not empty after sanitization
    if not name:
        name = "unnamed_dealer"
    return name

def load_json_file(filepath):
    """Loads data from a JSON file with error handling."""
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            return json.load(f)
    except FileNotFoundError:
        print(f"Error: File not found - {filepath}", file=sys.stderr)
        return None
    except json.JSONDecodeError:
        print(f"Error: Invalid JSON format in file - {filepath}", file=sys.stderr)
        return None
    except Exception as e:
        print(f"Error reading file {filepath}: {e}", file=sys.stderr)
        return None

def save_json_file(data, filepath):
    """Saves data to a JSON file with error handling."""
    try:
        # Create parent directories if they don't exist
        Path(filepath).parent.mkdir(parents=True, exist_ok=True)
        with open(filepath, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2) # Use indent for readability
        print(f"Successfully saved: {filepath}")
        return True
    except IOError as e:
        print(f"Error: Could not write file - {filepath}. Reason: {e}", file=sys.stderr)
        return False
    except Exception as e:
        print(f"An unexpected error occurred while saving {filepath}: {e}", file=sys.stderr)
        return False

# --- Core Logic ---

def split_dealers(input_file, output_dir):
    """
    Splits the dealers from the input empire JSON file into individual files.
    """
    print(f"Attempting to split dealers from: {input_file}")
    data = load_json_file(input_file)

    if data is None:
        return # Error message already printed by load_json_file

    if not isinstance(data, dict) or "dealers" not in data:
        print(f"Error: Input file {input_file} does not contain a 'dealers' key at the top level.", file=sys.stderr)
        return

    if not isinstance(data["dealers"], list):
        print(f"Error: The 'dealers' key in {input_file} does not contain a list.", file=sys.stderr)
        return

    output_path = Path(output_dir)
    output_path.mkdir(parents=True, exist_ok=True) # Ensure output directory exists

    count = 0
    for i, dealer in enumerate(data["dealers"]):
        if not isinstance(dealer, dict):
            print(f"Warning: Item at index {i} in 'dealers' is not a valid object. Skipping.", file=sys.stderr)
            continue

        dealer_name = dealer.get("name")
        if not dealer_name or not isinstance(dealer_name, str):
            print(f"Warning: Dealer at index {i} missing 'name' or name is not a string. Using index as name. Data: {dealer}", file=sys.stderr)
            dealer_name = f"dealer_{i}" # Fallback name

        sanitized_name = sanitize_filename(dealer_name)
        output_filename = f"{sanitized_name}{SPLIT_FILE_SUFFIX}"
        output_filepath = output_path / output_filename

        if save_json_file(dealer, output_filepath):
            count += 1

    print(f"\nSplit completed. {count} dealer files created in '{output_dir}'.")


def combine_dealers(input_dir, output_file):
    """
    Combines individual dealer JSON files from a directory into one empire file.
    """
    print(f"Attempting to combine dealers from directory: {input_dir}")
    input_path = Path(input_dir)
    output_filepath = Path(output_file)

    if not input_path.is_dir():
        print(f"Error: Input directory '{input_dir}' not found or is not a directory.", file=sys.stderr)
        return

    # Find files matching the split pattern in the input directory
    # Using Path.glob for better cross-platform compatibility
    dealer_files = list(input_path.glob(f"*{SPLIT_FILE_SUFFIX}"))

    if not dealer_files:
        print(f"No dealer files found in '{input_dir}' (expected pattern: *{SPLIT_FILE_SUFFIX}).", file=sys.stderr)
        return

    print(f"Found {len(dealer_files)} potential dealer files.")

    all_dealers = []
    processed_count = 0
    for file_path in dealer_files:
        # Ensure we don't accidentally read the output file if it's in the same dir
        # and somehow matches the pattern (unlikely with the suffix but safe check)
        if file_path.resolve() == output_filepath.resolve():
            print(f"Skipping potential input file as it matches the output file: {file_path}")
            continue

        print(f"Processing: {file_path.name}")
        dealer_data = load_json_file(file_path)
        if dealer_data is not None:
            # Basic validation: Check if it looks like a dealer object (has a 'name')
            if isinstance(dealer_data, dict) and "name" in dealer_data:
                 all_dealers.append(dealer_data)
                 processed_count += 1
            else:
                print(f"Warning: File {file_path.name} does not appear to contain a valid dealer object (missing 'name'?). Skipping.", file=sys.stderr)


    if not all_dealers:
        print("No valid dealer data found to combine.", file=sys.stderr)
        return

    # Structure the final output according to the schema
    empire_data = {"dealers": all_dealers}

    print(f"\nCombining {processed_count} dealers into: {output_file}")
    if save_json_file(empire_data, output_filepath):
        print("Combine operation successful.")
    else:
        print("Combine operation failed during file save.", file=sys.stderr)


# --- Main Execution ---

if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Split dealers from an empire JSON file or combine individual dealer JSON files.",
        epilog=f"Example Usage:\n"
               f"  Split:   python {sys.argv[0]} split -i {DEFAULT_EMPIRE_FILE} -o ./split_dealers\n"
               f"  Combine: python {sys.argv[0]} combine -d ./split_dealers -o {DEFAULT_EMPIRE_FILE}\n\n"
               f"Note: Combine looks for files ending with '{SPLIT_FILE_SUFFIX}' in the specified directory.",
        formatter_class=argparse.RawTextHelpFormatter # Keep newlines in help text
    )

    subparsers = parser.add_subparsers(dest="mode", required=True, help="Operation mode: 'split' or 'combine'")

    # --- Split Subparser ---
    parser_split = subparsers.add_parser("split", help="Split the main empire file into individual dealer files.")
    parser_split.add_argument(
        "-i", "--input",
        default=DEFAULT_EMPIRE_FILE,
        help=f"Path to the input empire JSON file (default: {DEFAULT_EMPIRE_FILE})"
    )
    parser_split.add_argument(
        "-o", "--output-dir",
        default=".", # Default to current directory
        help="Directory where individual dealer files will be saved (default: current directory)"
    )

    # --- Combine Subparser ---
    parser_combine = subparsers.add_parser("combine", help="Combine individual dealer files into one empire file.")
    parser_combine.add_argument(
        "-d", "--dir",
        required=True,
        help=f"Directory containing the individual dealer files (files ending with '{SPLIT_FILE_SUFFIX}')"
    )
    parser_combine.add_argument(
        "-o", "--output",
        default=DEFAULT_EMPIRE_FILE,
        help=f"Path to the output combined empire JSON file (default: {DEFAULT_EMPIRE_FILE})"
    )

    # --- Argument Parsing and Execution ---
    try:
        args = parser.parse_args()

        if args.mode == "split":
            split_dealers(args.input, args.output_dir)
        elif args.mode == "combine":
            combine_dealers(args.dir, args.output)

    except Exception as e:
        # Catch-all for unexpected errors during argument parsing or execution
        print(f"\nAn unexpected error occurred: {e}", file=sys.stderr)
        # Optionally print traceback for debugging:
        # import traceback
        # traceback.print_exc()
        sys.exit(1) # Exit with a non-zero code to indicate failure
