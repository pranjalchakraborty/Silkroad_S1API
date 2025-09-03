import json

def update_base_dollar(file_path: str):
    with open(file_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    if "dealers" in data:
        for dealer in data["dealers"]:
            # Get the first shipping's maxAmount; default to 1 if missing or zero
            shippings = dealer.get("shipping", [])
            if shippings and isinstance(shippings, list) and "maxAmount" in shippings[0]:
                max_amount = shippings[0].get("maxAmount", 1)
                if max_amount == 0:
                    max_amount = 1
            else:
                max_amount = 1

            # Update each drug's base_dollar
            drugs = dealer.get("drugs", [])
            for drug in drugs:
                base_dollar = drug.get("base_dollar", 0)
                # Divide by 2 and then by max_amount, then cast to int
                new_value = int(base_dollar / 2 / max_amount)
                drug["base_dollar"] = new_value

    # Write the updated data back to the file
    with open(file_path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=4)
    print(f"Updated base_dollar values in {file_path}")

if __name__ == "__main__":
    update_base_dollar("Empire-S1API/References/empire.json")