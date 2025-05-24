import json

input_file = r"c:\Users\Admin\Downloads\Test\Mine\Empire_S1API\Empire-S1API\References\empire.json"
output_file = r"c:\Users\Admin\Downloads\Test\Mine\Empire_S1API\Empire-S1API\References\empire_updated.json"

with open(input_file, "r") as f:
    data = json.load(f)

# Convert each element in effectsName to lowercase
if "effectsName" in data and isinstance(data["effectsName"], list):
    data["effectsName"] = [name.lower() for name in data["effectsName"]]

# For each dealer, each drug, and each effect object, convert the name to lowercase
if "dealers" in data and isinstance(data["dealers"], list):
    for dealer in data["dealers"]:
        if "drugs" in dealer and isinstance(dealer["drugs"], list):
            for drug in dealer["drugs"]:
                if "effects" in drug and isinstance(drug["effects"], list):
                    for effect in drug["effects"]:
                        if "name" in effect and isinstance(effect["name"], str):
                            effect["name"] = effect["name"].lower()

with open(output_file, "w") as f:
    json.dump(data, f, indent=4)

print(f"Updated JSON written to: {output_file}")