from pathlib import Path

controller = Path("YemenGuide.Web/Areas/Owner/Controllers/ReservationController.CustomerCancellation.cs")
text = controller.read_text(encoding="utf-8-sig")
old = "OwnerBookingCommissionStatuses."
new = "FinancialCommissionStatuses."
count = text.count(old)
if count != 4:
    raise SystemExit(f"expected 4 commission status references, found {count}")
controller.write_text(text.replace(old, new), encoding="utf-8-sig")

usings = Path("YemenGuide.Web/Areas/Owner/Controllers/ReservationController.CustomerCancellation.Usings.cs")
if usings.exists():
    usings.unlink()

print("finance constants corrected")
