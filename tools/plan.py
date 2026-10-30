"""Calls /shoppinglist/plan as a profile and prints a readable summary. Usage: plan.py <sessionId> [areaType ...]"""
import glob, json, ssl, sys, time, urllib.request, zlib

session = sys.argv[1]
areas = [int(a) for a in sys.argv[2:]]
ctx = ssl._create_unverified_context()

for _ in range(60):
    try:
        urllib.request.urlopen("https://127.0.0.1:6969/launcher/ping", context=ctx, timeout=3)
        break
    except urllib.error.HTTPError:
        break
    except Exception:
        time.sleep(3)

req = urllib.request.Request("https://127.0.0.1:6969/shoppinglist/plan", method="POST",
                             data=zlib.compress(json.dumps({"areas": areas, "crafts": True}).encode()),
                             headers={"Cookie": f"PHPSESSID={session}", "Content-Type": "application/json"})
raw = urllib.request.urlopen(req, context=ctx).read()
try:
    raw = zlib.decompress(raw)
except zlib.error:
    pass
plan = json.loads(raw)
if "error" in plan:
    sys.exit(plan["error"])

loc = json.load(open(glob.glob(r"C:\SPT\SPT_Runtime\SPT_Data\database\locales\global\en.json")[0], encoding="utf-8"))
name = lambda t: loc.get(f"{t} ShortName", t[:8])

for a in plan["areas"]:
    print(f"area {a['type']} -> L{a['level']} ready={a['ready']}: " + ", ".join(
        f"{name(i['tpl'])} {i['have']}/{i['required']}{' FIR' if i['fir'] else ''} [{i['plan']}]" for i in a["items"]))
for c in plan["crafts"]:
    print(f"craft {name(c['tpl'])} x{c['missing']} (area {c['area']}, {c['runs']} runs): " + ", ".join(
        f"{name(i['tpl'])} {i['have']}/{i['required']}{' tool' if i['tool'] else ''} [{i['plan']}]" for i in c["ingredients"]))
by_item = {}
for p in plan["purchases"]:
    key = (p["tpl"], p["source"])
    count, rub = by_item.get(key, (0, 0))
    by_item[key] = (count + p["count"], rub + p["rub"])
for (tpl, source), (count, rub) in by_item.items():
    print(f"buy {source:6} {name(tpl)} x{count} ~{rub} RUB")
print("unavailable", [(name(u["tpl"]), u["missing"]) for u in plan["unavailable"]])
print("cost", {name(k): v for k, v in plan["cost"].items()}, "money", {name(k): v for k, v in plan["money"].items()}, "totalRub", plan["totalRub"])
print("operations", [(o["Action"], len(o.get("offers", [])) or o.get("count")) for o in plan["operations"]])
