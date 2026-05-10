import re, json

def extract_json(file_path):
    with open(file_path, 'r', encoding='utf-16') as f:
        content = f.read()
        match = re.search(r'  "analyzed_offer": \{.*', content, re.DOTALL)
        if match:
            json_str = "{\n" + match.group(0).split('───')[0].strip()
            last_brace = json_str.rfind('}')
            json_str = json_str[:last_brace+1]
            try:
                return json.loads(json_str)
            except Exception as e:
                pass
    return None

data1 = extract_json('scratch/run1.log')
data2 = extract_json('scratch/run2.log')

if data1 and data2:
    print("RUN 1 ATS Score:", data1.get('cv_engine_result', {}).get('atsScore'))
    print("RUN 2 ATS Score:", data2.get('cv_engine_result', {}).get('atsScore'))
    print("\nRUN 1 Keywords (", len(data1.get('normalized_keywords', [])), "):", data1.get('normalized_keywords', []))
    print("RUN 2 Keywords (", len(data2.get('normalized_keywords', [])), "):", data2.get('normalized_keywords', []))
    print("\nRUN 1 Matched Skills (", len(data1.get('match_result', {}).get('matched_skills', [])), "):", data1.get('match_result', {}).get('matched_skills', []))
    print("RUN 2 Matched Skills (", len(data2.get('match_result', {}).get('matched_skills', [])), "):", data2.get('match_result', {}).get('matched_skills', []))
