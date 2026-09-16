from pathlib import Path


def replace_once(path: str, old: str, new: str, label: str) -> None:
    p = Path(path)
    text = p.read_text(encoding="utf-8-sig")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected one match, found {count}")
    p.write_text(text.replace(old, new, 1), encoding="utf-8-sig")


view_path = "YemenGuide.Web/Views/Places/Details.cshtml"
old_button = '''                <button type="button"
                        id="btnNextPackage"
                        onclick="@(isHall
                            ? "openHallModal()"
                            : "openAttendanceModal()")"
                        class="btn btn-info premium-modal-primary disabled"
                        disabled>
'''
new_button = '''                <button type="button"
                        id="btnNextPackage"
                        onclick="goToNextPackageStep(@(isHall ? "true" : "false"))"
                        class="btn btn-info premium-modal-primary disabled"
                        disabled>
'''
replace_once(view_path, old_button, new_button, "package next button")


js_path = "YemenGuide.Web/wwwroot/js/customer/Details.js"
old_signature = '''    async function checkPackageDateBeforeCheckout(
        placeId,
        packageId,
        date
    ) {
'''
new_signature = '''    window.goToNextPackageStep =
        async function goToNextPackageStep(
            isHall
        ) {
            const placeId =
                getRequiredPlaceId();

            const packageId =
                Number.parseInt(
                    String(
                        state.currentPackage.id ||
                        "0"
                    ),
                    10
                ) || 0;

            if (
                placeId <= 0 ||
                packageId <= 0 ||
                !state.packageDate
            ) {
                await showPackageAvailabilityAlert(
                    "اختر يوم الحجز أولًا ثم حاول مرة أخرى.",
                    false
                );
                return;
            }

            const isDateAvailable =
                await checkPackageDateBeforeCheckout(
                    placeId,
                    packageId,
                    state.packageDate,
                    true
                );

            if (!isDateAvailable) {
                return;
            }

            if (isHall === true) {
                window.openHallModal();
            } else {
                window.openAttendanceModal();
            }
        };

    async function checkPackageDateBeforeCheckout(
        placeId,
        packageId,
        date,
        showStartedWarning = true
    ) {
'''
replace_once(js_path, old_signature, new_signature, "availability signature")

old_started = '''            if (data?.started === true) {
'''
new_started = '''            if (
                showStartedWarning &&
                data?.started === true
            ) {
'''
replace_once(js_path, old_started, new_started, "started warning gate")

old_final = '''                await checkPackageDateBeforeCheckout(
                    placeId,
                    packageId,
                    state.packageDate
                );
'''
new_final = '''                await checkPackageDateBeforeCheckout(
                    placeId,
                    packageId,
                    state.packageDate,
                    false
                );
'''
p = Path(js_path)
text = p.read_text(encoding="utf-8-sig")
count = text.count(old_final)
if count != 2:
    raise SystemExit(f"final availability calls: expected two matches, found {count}")
p.write_text(text.replace(old_final, new_final), encoding="utf-8-sig")
