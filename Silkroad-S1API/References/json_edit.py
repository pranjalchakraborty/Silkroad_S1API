import json

with open("empire.json", "r", encoding="utf-8") as f:
    data = json.load(f)

for dealer in data.get("dealers", []):
    for drug in dealer.get("drugs", []):
        if "effects" in drug:
            for effect in drug["effects"]:
                if "type" in effect:
                    effect["name"] = effect.pop("type")

with open("empire.json", "w", encoding="utf-8") as f:
    json.dump(data, f, indent=4)