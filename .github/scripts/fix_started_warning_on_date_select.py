from pathlib import Path

path = Path('YemenGuide.Web/wwwroot/js/customer/Details.js')
text = path.read_text(encoding='utf-8-sig')

old_onchange = '''                    onChange:
                        (
                            selectedDates,
                            dateString
                        ) => {
                            const selected =
                                selectedDates.length >
                                0;

                            state.packageDate =
                                selected
                                    ? dateString
                                    : "";

                            setPackageNextEnabled(
                                selected
                            );
                        }
'''

new_onchange = '''                    onChange:
                        async (
                            selectedDates,
                            dateString,
                            instance
                        ) => {
                            const selected =
                                selectedDates.length >
                                0;

                            state.packageDate =
                                selected
                                    ? dateString
                                    : "";

                            setPackageNextEnabled(
                                false
                            );

                            if (!selected) {
                                return;
                            }

                            const selectedKey =
                                `${placeId}:${packageId}:${dateString}`;

                            if (
                                state.startedPeriodAcknowledgedKey &&
                                state.startedPeriodAcknowledgedKey !== selectedKey
                            ) {
                                state.startedPeriodAcknowledgedKey =
                                    "";
                            }

                            const isDateAvailable =
                                await checkPackageDateBeforeCheckout(
                                    placeId,
                                    packageId,
                                    dateString,
                                    true
                                );

                            if (!isDateAvailable) {
                                state.packageDate =
                                    "";

                                try {
                                    instance.clear(false);
                                } catch {
                                    // تجاهل
                                }

                                setPackageNextEnabled(
                                    false
                                );

                                return;
                            }

                            setPackageNextEnabled(
                                true
                            );
                        }
'''

if text.count(old_onchange) != 1:
    raise SystemExit(f'onChange block expected once, found {text.count(old_onchange)}')
text = text.replace(old_onchange, new_onchange, 1)

old_condition = '''            if (
                showStartedWarning &&
                data?.started === true
            ) {
'''
new_condition = '''            const startedPeriodKey =
                `${placeId}:${packageId}:${date}`;

            if (
                showStartedWarning &&
                data?.started === true &&
                state.startedPeriodAcknowledgedKey !== startedPeriodKey
            ) {
'''
if text.count(old_condition) != 1:
    raise SystemExit(f'started condition expected once, found {text.count(old_condition)}')
text = text.replace(old_condition, new_condition, 1)

old_confirm = '''                if (
                    !result ||
                    result.isConfirmed !== true
                ) {
                    reopenPackageDatePicker();
                    return false;
                }
            }

            return true;
'''
new_confirm = '''                if (
                    !result ||
                    result.isConfirmed !== true
                ) {
                    state.startedPeriodAcknowledgedKey =
                        "";

                    reopenPackageDatePicker();
                    return false;
                }

                state.startedPeriodAcknowledgedKey =
                    startedPeriodKey;
            }

            return true;
'''
if text.count(old_confirm) != 1:
    raise SystemExit(f'confirm block expected once, found {text.count(old_confirm)}')
text = text.replace(old_confirm, new_confirm, 1)

path.write_text(text, encoding='utf-8-sig')
