import psycopg2

try:
    conn = psycopg2.connect("postgresql://postgres:postgres@localhost:5432/nextstep")
    cur = conn.cursor()
    
    print("=== SESSIONS ===")
    cur.execute("SELECT id_session, id_utilisateur, mode, domain, id_candidature FROM public.session_coaching;")
    sessions = cur.fetchall()
    for s in sessions:
        print(f"SessionID: {s[0]} | UserID: {s[1]} | Mode: {s[2]} | Domain: {s[3]} | CandID: {s[4]}")
        
    print("\n=== CANDIDATURES ===")
    cur.execute("SELECT id_candidature, id_utilisateur, id_offre FROM public.candidature;")
    cands = cur.fetchall()
    for c in cands:
        print(f"CandID: {c[0]} | UserID: {c[1]} | OfferID: {c[2]}")
        
    print("\n=== OFFRES ANALYSEES ===")
    cur.execute("SELECT id, id_offre, titre_poste, entreprise FROM public.offre_analysee;")
    offres = cur.fetchall()
    for o in offres:
        print(f"AnalyseeID: {o[0]} | OfferID: {o[1]} | Title: {o[2]} | Company: {o[3]}")
        
    cur.close()
    conn.close()
except Exception as e:
    print("Error:", e)
