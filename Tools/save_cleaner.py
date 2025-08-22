import json
from collections import Counter

def remove_all_instances_of_duplicates(file_path='NPCs.json'):
    """
    Reads an NPC data file, identifies NPCs with duplicate IDs, removes all
    instances of those duplicates, and overwrites the file with the cleaned data.
    """
    try:
        # Open and read the JSON file
        with open(file_path, 'r') as f:
            data = json.load(f)

        # Ensure the 'NPCs' key exists and is a list
        if 'NPCs' not in data or not isinstance(data['NPCs'], list):
            print("Error: The JSON file must contain a top-level key 'NPCs' with a list of NPC objects.")
            return
            
        original_npc_list = data['NPCs']
        all_npc_ids = []
        
        # First pass: Extract all NPC IDs
        for npc in original_npc_list:
            try:
                base_data_str = npc.get('BaseData')
                if not base_data_str:
                    # Treat NPCs without BaseData as unique to avoid accidental removal
                    all_npc_ids.append(None) 
                    continue
                
                base_data = json.loads(base_data_str)
                all_npc_ids.append(base_data.get('ID'))

            except (json.JSONDecodeError, AttributeError):
                # Also treat malformed NPCs as unique
                all_npc_ids.append(None)

        # Count the occurrences of each ID
        id_counts = Counter(all_npc_ids)
        
        # Identify which IDs are duplicates (appear more than once)
        # We also exclude 'None' in case of malformed data
        ids_to_remove = {npc_id for npc_id, count in id_counts.items() if count > 1 and npc_id is not None}
        
        if not ids_to_remove:
            print("\nNo duplicate NPCs found. The file was not changed.")
            return

        print(f"Found duplicate entries for the following NPC IDs: {', '.join(ids_to_remove)}")
        print("Removing all instances of these NPCs...")

        # Second pass: Build the new list, excluding all instances of the duplicate IDs
        final_npcs = []
        for npc in original_npc_list:
            try:
                base_data_str = npc.get('BaseData')
                if not base_data_str:
                    final_npcs.append(npc) # Keep NPCs without BaseData
                    continue

                base_data = json.loads(base_data_str)
                npc_id = base_data.get('ID')
                
                # Only add the NPC if its ID is NOT in the set of duplicates
                if npc_id not in ids_to_remove:
                    final_npcs.append(npc)

            except (json.JSONDecodeError, AttributeError):
                final_npcs.append(npc) # Keep malformed NPCs

        # Update the original data structure
        original_count = len(data['NPCs'])
        final_count = len(final_npcs)
        data['NPCs'] = final_npcs
        
        # Write the updated data back to the same file
        with open(file_path, 'w') as f:
            json.dump(data, f, indent=4)
        
        print(f"\nSuccessfully removed {original_count - final_count} NPC object(s).")
        print(f"The file '{file_path}' has been updated.")

    except FileNotFoundError:
        print(f"Error: The file '{file_path}' was not found in the same directory.")
    except json.JSONDecodeError:
        print(f"Error: Could not decode the JSON from the file '{file_path}'. Please check its format.")
    except Exception as e:
        print(f"An unexpected error occurred: {e}")

# --- Execute the function ---
if __name__ == "__main__":
    remove_all_instances_of_duplicates()