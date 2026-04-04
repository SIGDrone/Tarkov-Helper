import sqlite3
import os

db_path = r"C:\Users\jonam\.gemini\antigravity\scratch\Tarkov-Item-Helper-main\TarkovHelper\Assets\tarkov_data.db"
if not os.path.exists(db_path):
    print("DB not found")
else:
    conn = sqlite3.connect(db_path)
    cur = conn.cursor()
    cur.execute("SELECT DISTINCT Location FROM Quests")
    rows = cur.fetchall()
    print("Unique Locations in DB:")
    for row in rows:
        print(row[0])
