import re

def remove_accent_and_arcs(ipa_text):
    cleaned_ipa = re.sub(r'[ˈˌ͡ː]', '', ipa_text)
    return cleaned_ipa

def ipa_to_cvcv(ipa_text):
    # Vowels
    cvcv_ipa = ipa_text.replace('ə', '@').replace('ɜ', '@').replace('ɛ', 'E').replace('ɪ', 'I').replace('ɔ', 'O').replace('ʊ', 'U').replace('œ', 'oe').replace('ø', '0')
    # Consonants
    cvcv_ipa = cvcv_ipa.replace('ç', 'ch').replace('dʒ', 'dZ').replace('ŋ', 'N').replace('ɾ', 'R').replace('ts', '1').replace('s', 'ss').replace('z', 's').replace('ʃ', 'S').replace('v', 'w').replace('1', 'z')
    return cvcv_ipa

print("Please use an external source like unalengua to convert your lyrics to IPA.\n")

ipa_text = input("IPA: ")
cleaned_ipa = remove_accent_and_arcs(ipa_text)
cvcv_ipa = ipa_to_cvcv(cleaned_ipa)
print("CVCV: ", cvcv_ipa)

input()
