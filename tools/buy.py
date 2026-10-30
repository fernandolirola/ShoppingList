"""Plans for the given areas, then sends the plan's operations the way the game client does, re-planning
leftovers like the client will. Usage: buy.py <sessionId> areaType..."""
import json, ssl, sys, time, urllib.error, urllib.request, zlib

session = sys.argv[1]
areas = [int(a) for a in sys.argv[2:]]
ctx = ssl._create_unverified_context()


def post(path, body):
    req = urllib.request.Request(f"https://127.0.0.1:6969{path}", method="POST", data=zlib.compress(json.dumps(body).encode()),
                                 headers={"Cookie": f"PHPSESSID={session}", "Content-Type": "application/json"})
    raw = urllib.request.urlopen(req, context=ctx).read()
    try:
        raw = zlib.decompress(raw)
    except zlib.error:
        pass
    return json.loads(raw)


def missing(plan):
    return sum(i["missing"] for a in plan["areas"] for i in a["items"] if i["plan"] == "buy") + \
        sum(i["missing"] for c in plan["crafts"] for i in c["ingredients"])


for _ in range(60):
    try:
        urllib.request.urlopen("https://127.0.0.1:6969/launcher/ping", context=ctx, timeout=3)
        break
    except urllib.error.HTTPError:
        break
    except Exception:
        time.sleep(3)

plan = post("/shoppinglist/plan", {"areas": areas, "crafts": True})
print("before: buyable missing", missing(plan), "cost", plan["cost"], "money", plan["money"])
for round_no in range(3):
    ok = failed = added = 0
    for op in plan["operations"]:
        res = post("/client/game/profile/items/moving", {"data": [op], "tm": 0, "reload": 0})
        data = res.get("data") or {}
        changes = next(iter((data.get("profileChanges") or {}).values()), {})
        if data.get("warnings"):
            failed += 1
            print("  warn", op["Action"], data["warnings"][0].get("errmsg"))
        else:
            ok += 1
            added += len(changes.get("items", {}).get("new", []))
    plan = post("/shoppinglist/plan", {"areas": areas, "crafts": True})
    print(f"round {round_no + 1}: ok {ok}, failed {failed}, new stacks {added}; buyable missing now {missing(plan)}")
    if failed == 0 or not plan["operations"]:
        break

print("after: money", plan["money"], "unavailable", [(u["tpl"][:6], u["missing"]) for u in plan["unavailable"]])
for a in plan["areas"]:
    print("area", a["type"], [(i["tpl"][:6], f"{i['have']}/{i['required']}", i["plan"]) for i in a["items"]])
for c in plan["crafts"]:
    print("craft", c["tpl"][:6], "runs", c["runs"], [(i["tpl"][:6], f"{i['have']}/{i['required']}", i["plan"]) for i in c["ingredients"]])
