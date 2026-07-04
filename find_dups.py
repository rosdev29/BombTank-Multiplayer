import sys
import re
from collections import defaultdict

def find_duplicates(file_path):
    ids = defaultdict(list)
    pattern = re.compile(r"^--- !u!\d+ &(\d+)")
    
    with open(file_path, 'r', encoding='utf-8') as f:
        for line_num, line in enumerate(f, 1):
            match = pattern.match(line)
            if match:
                file_id = match.group(1)
                ids[file_id].append(line_num)
                
    duplicates = {fid: lines for fid, lines in ids.items() if len(lines) > 1}
    for fid, lines in duplicates.items():
        print(f"ID {fid} is duplicated on lines: {lines}")
    
    print(f"Total duplicate IDs: {len(duplicates)}")

if __name__ == "__main__":
    find_duplicates(r"e:\2026\game\BombTank-Multiplayer\Assets\Scenes\Game.unity")
