/* eslint-env browser */
/* global document, window, CSS */
/* @noflow */
/* exported initQuestionnaireAssignmentEditor */

function clamp(v, min, max) {
    return Math.max(min, Math.min(max, v));
}

function clearElement(el) {
    while (el && el.firstChild) {
        el.removeChild(el.firstChild);
    }
}

function mapGet(map, key, defaultValue) {
    return map.has(key) ? map.get(key) : defaultValue;
}

function mapSet(map, key, value) {
    map.set(key, value);
}

function mapDelete(map, key) {
    map.delete(key);
}

function mapIsTrue(map, key) {
    return map.get(key) === true;
}

function parseIntSafe(raw, fallback) {
    var parsed = parseInt(raw, 10);
    return Number.isNaN(parsed) ? fallback : parsed;
}

function getFirstElement(list) {
    var first = null;
    list.forEach((item) => {
        if (first === null) {
            first = item;
        }
    });
    return first;
}

function getItemAtIndex(list, index) {
    var found = null;
    var pos = 0;
    list.forEach((item) => {
        if (pos === index) {
            found = item;
        }
        pos += 1;
    });
    return found;
}

function incrementModulo(list, times, onItem) {
    if (!list.length || times <= 0) {
        return;
    }
    var len = list.length;
    var step = 0;
    while (step < times) {
        var target = getItemAtIndex(list, step % len);
        if (target) {
            onItem(target);
        }
        step += 1;
    }
}

function findWeightRow(container, questionId) {
    if (!container) {
        return null;
    }
    var targetId = String(questionId);
    var match = null;
    container.querySelectorAll('.question-weight-row[data-question-id]').forEach((row) => {
        if (!match && row.getAttribute('data-question-id') === targetId) {
            match = row;
        }
    });
    return match;
}

function initQuestionnaireAssignmentEditor(options) {
    options = options || {};
        var formIdOption = options.formId;
        var stageCountInputIdOption = options.stageCountInputId;
        var multiStageToggleFieldIdOption = options.multiStageToggleFieldId;
        var stageCountSectionIdOption = options.stageCountSectionId;
        var form = document.getElementById(formIdOption) || document.getElementById('positionCreateForm') || document.getElementById('positionEditForm') || document.getElementById('templateEditForm');
        if (!form) {
            return undefined;
        }
        var weightsFieldId = 'questionWeightValues';
        var weightsFieldEl = document.getElementById(weightsFieldId);
        var stageValuesFieldId = 'questionStagesPayload';
        var stagesPayloadInput = document.getElementById(stageValuesFieldId);
        var stageCountInput = document.getElementById(stageCountInputIdOption || 'questionnaireStageCountInput');
        var multiStageToggleFieldId = multiStageToggleFieldIdOption || 'enableMultiStageToggle';
        var secondaryStageToggleEl = document.getElementById(multiStageToggleFieldId);
        var stageCountSection = document.getElementById(stageCountSectionIdOption || 'questionnaireStageCountSection');
        var legendRow = document.getElementById('questionnaireStageLegendRow');
        var legendEl = document.getElementById('questionnaireStageLegend');
        var budgetText = document.getElementById('questionWeightBudgetText');
        var budgetBar = document.getElementById('questionWeightBudgetBar');
        var redistributeButton = document.getElementById('questionWeightRedistributeBtn');
        var weightRows = document.getElementById('questionWeightRows');
        var weightEmpty = document.getElementById('questionWeightEmpty');
        var questionCheckboxes = Array.prototype.slice.call(document.querySelectorAll('.question-checkbox'));
        var selectedQuestionsHiddenContainer = document.getElementById('selectedQuestionsHiddenContainer');
        var questionWeights = new Map();
        var questionLocks = new Map();
        var questionStages = new Map();
        var isQuestionSelected = new Map();
        var activeQuestionnaireEditorStage = 1;

        function readStageCountFromInput() {
            if (!stageCountInput) {
                return 1;
            }
            var n = parseInt(stageCountInput.value, 10);
            if (Number.isNaN(n) || n < 1) {
                return 1;
            }
            if (n > 10) {
                return 10;
            }
            return n;
        }

        function getQuestionnaireStageCount() {
            if (secondaryStageToggleEl && !secondaryStageToggleEl.checked) {
                return 1;
            }
            return readStageCountFromInput();
        }

        function clampActiveEditorStage(maxStage) {
            if (activeQuestionnaireEditorStage > maxStage) {
                activeQuestionnaireEditorStage = maxStage;
            }
            if (activeQuestionnaireEditorStage < 1) {
                activeQuestionnaireEditorStage = 1;
            }
        }

        function refreshStageSwitcher() {
            if (!legendRow || !legendEl) {
                return;
            }
            var n = getQuestionnaireStageCount();
            clearElement(legendEl);
            if (n <= 1) {
                legendRow.classList.add('d-none');
                return;
            }
            clampActiveEditorStage(n);
            legendRow.classList.remove('d-none');
            for (var s = 1; s <= n; s++) {
                (function (stageNum) {
                    var btn = document.createElement('button');
                    btn.type = 'button';
                    btn.className = 'questionnaire-stage-switcher-btn' + (stageNum === activeQuestionnaireEditorStage ? ' active' : '');
                    btn.setAttribute('role', 'tab');
                    btn.setAttribute('aria-selected', stageNum === activeQuestionnaireEditorStage ? 'true' : 'false');
                    btn.textContent = 'Stage ' + stageNum;
                    btn.addEventListener('click', () => {
                        activeQuestionnaireEditorStage = stageNum;
                        refreshStageSwitcher();
                        syncAllCheckboxVisuals();
                        syncGroupSelectHeaders();
                    });
                    legendEl.appendChild(btn);
                })(s);
            }
        }

        function selectedCheckboxes() {
            return questionCheckboxes.filter((cb) => {
                return mapIsTrue(isQuestionSelected, cb.value);
            });
        }

        function syncCheckboxVisualFor(cb) {
            var qid = cb.value;
            var n = getQuestionnaireStageCount();
            if (n <= 1) {
                cb.checked = mapIsTrue(isQuestionSelected, qid);
                return;
            }
            cb.checked = mapIsTrue(isQuestionSelected, qid) &&
                parseInt(mapGet(questionStages, qid, 1), 10) === activeQuestionnaireEditorStage;
        }

        function syncAllCheckboxVisuals() {
            questionCheckboxes.forEach(syncCheckboxVisualFor);
        }

        function syncGroupSelectHeaders() {
            Array.prototype.slice.call(document.querySelectorAll('.group-select-all')).forEach((groupSelect) => {
                var targetGroupClass = groupSelect.getAttribute('data-target-group') + '-checkbox';
                var targetBoxes = Array.prototype.slice.call(document.querySelectorAll('.' + targetGroupClass));
                if (targetBoxes.length) {
                    groupSelect.checked = targetBoxes.every((box) => {
                        return box.checked;
                    });
                }
            });
        }

        function refreshQuestionnaireStageUi() {
            refreshStageSwitcher();
            syncAllCheckboxVisuals();
            syncGroupSelectHeaders();
            updateStagesPayload(selectedCheckboxes());
        }

        function updateStageCountSectionVisibility() {
            if (stageCountSection && secondaryStageToggleEl) {
                if (secondaryStageToggleEl.checked) {
                    stageCountSection.classList.remove('d-none');
                } else {
                    stageCountSection.classList.add('d-none');
                }
            }
        }

        function rebuildSelectedQuestionsHiddenInputs() {
            if (!selectedQuestionsHiddenContainer) {
                return;
            }
            clearElement(selectedQuestionsHiddenContainer);
            questionCheckboxes.forEach((cb) => {
                var qid = cb.value;
                if (!mapIsTrue(isQuestionSelected, qid)) {
                    return;
                }
                var h = document.createElement('input');
                h.type = 'hidden';
                h.name = 'selectedQuestions';
                h.value = qid;
                selectedQuestionsHiddenContainer.appendChild(h);
            });
        }

        function updateStagesPayload(selected) {
            if (!stagesPayloadInput) {
                return;
            }
            if (!selected.length) {
                stagesPayloadInput.value = '';
                return;
            }
            var maxS = getQuestionnaireStageCount();
            if (maxS <= 1) {
                stagesPayloadInput.value = '';
                return;
            }
            var pairs = selected.map((cb) => {
                var qid = cb.value;
                var st = parseInt(mapGet(questionStages, qid, 1), 10);
                if (Number.isNaN(st) || st < 1) {
                    st = 1;
                }
                if (st > maxS) {
                    st = maxS;
                }
                mapSet(questionStages, qid, st);
                return qid + '=' + st;
            });
            stagesPayloadInput.value = pairs.join(';');
        }

        function selectedIds(selected) {
            return selected.map((cb) => { return cb.value; });
        }

        function ensureWeight(questionId) {
            var current = parseIntSafe(mapGet(questionWeights, questionId, 0), NaN);
            if (Number.isNaN(current)) {
                current = 0;
            }
            mapSet(questionWeights, questionId, clamp(current, 0, 100));
        }

        function sumByIds(questionIds) {
            return questionIds.reduce((sum, questionId) => {
                ensureWeight(questionId);
                return sum + mapGet(questionWeights, questionId, 0);
            }, 0);
        }

        function normalizeIntegerDistribution(questionIds, targetTotal, basisAccessor) {
            var sanitizedTarget = clamp(parseIntSafe(targetTotal, 0), 0, 100);
            if (!questionIds.length) {
                return new Map();
            }

            if (questionIds.length === 1) {
                var single = new Map();
                single.set(getFirstElement(questionIds), sanitizedTarget);
                return single;
            }

            var basis = questionIds.map((questionId) => {
                var raw = basisAccessor(questionId);
                var safe = clamp(parseIntSafe(raw, 0), 0, 100);
                return { id: questionId, weight: safe };
            });

            var totalBasis = basis.reduce((sum, item) => { return sum + item.weight; }, 0);
            if (totalBasis <= 0) {
                var evenBase = Math.floor(sanitizedTarget / questionIds.length);
                var evenRemainder = sanitizedTarget - (evenBase * questionIds.length);
                var evenResult = new Map();
                questionIds.forEach((questionId, index) => {
                    evenResult.set(questionId, evenBase + (index < evenRemainder ? 1 : 0));
                });
                return evenResult;
            }

            var allocation = basis.map((item, index) => {
                var exact = (item.weight / totalBasis) * sanitizedTarget;
                var floored = Math.floor(exact);
                return {
                    id: item.id,
                    index: index,
                    value: floored,
                    fraction: exact - floored
                };
            });

            var assigned = allocation.reduce((sum, item) => { return sum + item.value; }, 0);
            var remainder = sanitizedTarget - assigned;
            allocation.sort((a, b) => { return b.fraction - a.fraction; });
            incrementModulo(allocation, remainder, (item) => {
                item.value += 1;
            });
            allocation.sort((a, b) => { return a.index - b.index; });

            var result = new Map();
            allocation.forEach((item) => {
                result.set(item.id, clamp(item.value, 0, 100));
            });
            return result;
        }

        function updatePayload(selected) {
            var pairs = selected.map((cb) => {
                return cb.value + '=' + (mapGet(questionWeights, cb.value, 0));
            });
            weightsFieldEl.value = pairs.join(';');
        }

        function updateBudgetUi(selected) {
            var total = selected.reduce((sum, cb) => {
                ensureWeight(cb.value);
                return sum + mapGet(questionWeights, cb.value, 0);
            }, 0);
            var boundedTotal = clamp(total, 0, 100);
            budgetText.textContent = total + ' / 100 allocated';
            budgetBar.style.width = boundedTotal + '%';
        }

        function updateRedistributeButtonState(selected) {
            if (!redistributeButton) {
                return;
            }

            var unlockedCount = selected.filter((cb) => {
                return !mapIsTrue(questionLocks, cb.value);
            }).length;

            redistributeButton.disabled = !selected.length || unlockedCount === 0;
        }

        function redistributeUnlockedEvenly(selected) {
            if (!selected.length) {
                return;
            }

            var ids = selectedIds(selected);
            ids.forEach(ensureWeight);

            var lockedIds = ids.filter((questionId) => { return mapIsTrue(questionLocks, questionId); });
            var unlockedIds = ids.filter((questionId) => { return !mapIsTrue(questionLocks, questionId); });
            if (!unlockedIds.length) {
                return;
            }

            var lockedTotal = sumByIds(lockedIds);
            if (lockedTotal > 100) {
                var compressedLocked = normalizeIntegerDistribution(lockedIds, 100, (questionId) => { return mapGet(questionWeights, questionId, 0); });
                lockedIds.forEach((questionId) => { mapSet(questionWeights, questionId, compressedLocked.get(questionId)); });
                lockedTotal = 100;
            }

            var unlockedTarget = clamp(100 - lockedTotal, 0, 100);
            var base = Math.floor(unlockedTarget / unlockedIds.length);
            var remainder = unlockedTarget - (base * unlockedIds.length);
            unlockedIds.forEach((questionId, index) => {
                mapSet(questionWeights, questionId, base + (index < remainder ? 1 : 0));
            });
        }

        function syncWeightUi(selected) {
            if (!selected.length) {
                weightEmpty.style.display = '';
                budgetText.textContent = '0 / 100 allocated';
                budgetBar.style.width = '0%';
                weightsFieldEl.value = '';
                if (stagesPayloadInput) {
                    stagesPayloadInput.value = '';
                }
                updateRedistributeButtonState(selected);
                refreshQuestionnaireStageUi();
                return;
            }

            weightEmpty.style.display = 'none';
            selected.forEach((cb) => {
                var questionId = cb.value;
                ensureWeight(questionId);
                var row = findWeightRow(weightRows, questionId);
                if (!row) {
                    return;
                }

                var locked = mapIsTrue(questionLocks, questionId);
                row.classList.toggle('locked', locked);

                var slider = row.querySelector('.question-weight-slider');
                var number = row.querySelector('.question-weight-number');
                var value = row.querySelector('.question-weight-value');
                var icon = row.querySelector('.question-weight-lock i');
                var current = mapGet(questionWeights, questionId, 0);

                if (slider) {
                    slider.value = current;
                    slider.disabled = locked;
                }

                if (number) {
                    number.value = current;
                    number.disabled = locked;
                }

                if (value) {
                    value.textContent = current + ' pts';
                }

                if (icon) {
                    icon.classList.toggle('fa-lock', locked);
                    icon.classList.toggle('fa-lock-open', !locked);
                }

                var lockBtn = row.querySelector('.question-weight-lock');
                if (lockBtn) {
                    lockBtn.title = locked ? 'Unlock to adjust this weight with the sliders' : 'Lock to keep this weight fixed while adjusting others';
                }
            });

            updateBudgetUi(selected);
            updatePayload(selected);
            updateStagesPayload(selected);
            updateRedistributeButtonState(selected);
            refreshQuestionnaireStageUi();
        }

        function setFocusedWeight(questionId, rawValue) {
            var selected = selectedCheckboxes();
            if (!selected.length) {
                return;
            }

            if (mapIsTrue(questionLocks, questionId)) {
                syncWeightUi(selected);
                return;
            }

            var ids = selectedIds(selected);
            ids.forEach(ensureWeight);

            function sumWeightsByIds(questionIds) {
                return questionIds.reduce((sum, id) => {
                    ensureWeight(id);
                    return sum + (mapGet(questionWeights, id, 0));
                }, 0);
            }

            function reduceUnlockedPool(poolIds, reductionNeeded) {
                if (!poolIds.length || reductionNeeded <= 0) {
                    return;
                }

                var pool = poolIds
                    .map((id, index) => {
                        ensureWeight(id);
                        return { id: id, index: index, weight: mapGet(questionWeights, id, 0) };
                    })
                    .filter((item) => { return item.weight > 0; });

                if (!pool.length) {
                    return;
                }

                var poolTotal = pool.reduce((sum, item) => { return sum + item.weight; }, 0);
                if (poolTotal <= 0) {
                    return;
                }

                var reductions = pool.map((item) => {
                    var exact = (item.weight / poolTotal) * reductionNeeded;
                    var floored = Math.floor(exact);
                    return {
                        id: item.id,
                        index: item.index,
                        value: Math.min(item.weight, floored),
                        fraction: exact - floored,
                        max: item.weight
                    };
                });

                var assigned = reductions.reduce((sum, item) => { return sum + item.value; }, 0);
                var remainder = reductionNeeded - assigned;
                reductions.sort((a, b) => { return b.fraction - a.fraction; });

                incrementModulo(reductions, remainder, (pick) => {
                    if (pick.value < pick.max) {
                        pick.value += 1;
                    }
                });

                reductions.sort((a, b) => { return a.index - b.index; });
                reductions.forEach((item) => {
                    mapSet(questionWeights, item.id, clamp((mapGet(questionWeights, item.id, 0)) - item.value, 0, 100));
                });
            }

            var lockedIds = ids.filter((id) => { return mapIsTrue(questionLocks, id); });
            var lockedTotal = sumWeightsByIds(lockedIds);
            var maxForThis = clamp(100 - lockedTotal, 0, 100);

            var current = mapGet(questionWeights, questionId, 0);
            var desired = clamp(parseIntSafe(rawValue, 0), 0, maxForThis);

            if (desired <= current) {
                mapSet(questionWeights, questionId, desired);
                syncWeightUi(selected);
                return;
            }

            mapSet(questionWeights, questionId, desired);
            var otherUnlockedIds = ids.filter((id) => { return id !== questionId && !mapIsTrue(questionLocks, id); });
            var otherUnlockedTotal = sumWeightsByIds(otherUnlockedIds);
            var totalAfter = lockedTotal + desired + otherUnlockedTotal;
            var reductionNeeded = Math.max(0, totalAfter - 100);
            reduceUnlockedPool(otherUnlockedIds, reductionNeeded);

            syncWeightUi(selected);
        }

        function toggleLock(questionId) {
            mapSet(questionLocks, questionId, !mapIsTrue(questionLocks, questionId));
            var selected = selectedCheckboxes();
            syncWeightUi(selected);
        }

        function createWeightRow(cb) {
            var questionId = cb.value;
            var questionText = cb.getAttribute('data-question-text') || ('Question ' + questionId);
            ensureWeight(questionId);

            var row = document.createElement('div');
            row.className = 'question-weight-row';
            row.setAttribute('data-question-id', questionId);

            var title = document.createElement('div');
            title.className = 'small mb-2';
            title.textContent = questionText;

            var controls = document.createElement('div');
            controls.className = 'd-flex align-items-center';

            var lockButton = document.createElement('button');
            lockButton.type = 'button';
            lockButton.className = 'btn btn-sm btn-outline-secondary question-weight-lock mr-2';
            lockButton.title = 'Lock weight';
            var lockIcon = document.createElement('i'); lockIcon.className = 'fas fa-lock-open'; lockButton.appendChild(lockIcon);
            lockButton.addEventListener('click', () => {
                toggleLock(questionId);
            });

            var slider = document.createElement('input');
            slider.type = 'range';
            slider.min = '0';
            slider.max = '100';
            slider.step = '1';
            slider.className = 'flex-grow-1 question-weight-slider';
            slider.value = mapGet(questionWeights, questionId, 0);
            slider.addEventListener('input', () => {
                setFocusedWeight(questionId, slider.value);
            });
            slider.addEventListener('change', () => {
                setFocusedWeight(questionId, slider.value);
            });

            var number = document.createElement('input');
            number.type = 'number';
            number.min = '0';
            number.max = '100';
            number.step = '1';
            number.className = 'form-control form-control-sm ml-2 question-weight-number';
            number.style.maxWidth = '72px';
            number.value = mapGet(questionWeights, questionId, 0);
            number.addEventListener('input', () => {
                setFocusedWeight(questionId, number.value);
            });
            number.addEventListener('change', () => {
                setFocusedWeight(questionId, number.value);
            });

            var value = document.createElement('span');
            value.className = 'question-weight-value ml-2';
            value.textContent = mapGet(questionWeights, questionId, 0) + ' pts';

            controls.appendChild(lockButton);
            controls.appendChild(slider);
            controls.appendChild(number);
            controls.appendChild(value);

            row.appendChild(title);
            row.appendChild(controls);

            return row;
        }

        function renderRows() {
            var selected = selectedCheckboxes();
            clearElement(weightRows);

            if (!selected.length) {
                syncWeightUi(selected);
                return;
            }

            selected.forEach((cb) => {
                ensureWeight(cb.value);
                var qid = cb.value;
                if (!questionStages.has(qid) || Number.isNaN(parseInt(mapGet(questionStages, qid, 1), 10))) {
                    mapSet(questionStages, qid, 1);
                }
            });

            var ids = selectedIds(selected);
            var total = ids.reduce((sum, id) => { return sum + (mapGet(questionWeights, id, 0)); }, 0);
            if (total <= 0) {
                if (ids.length === 1) {
                    mapSet(questionWeights, getFirstElement(ids), 100);
                } else {
                    var base = Math.floor(100 / ids.length);
                    var rem = 100 - (base * ids.length);
                    ids.forEach((id, idx) => {
                        mapSet(questionWeights, id, base + (idx < rem ? 1 : 0));
                    });
                }
            }

            selected.forEach((cb) => {
                weightRows.appendChild(createWeightRow(cb));
            });

            syncWeightUi(selected);
        }

        var groupSelects = Array.prototype.slice.call(document.querySelectorAll('.group-select-all'));
        groupSelects.forEach((groupSelect) => {
            groupSelect.addEventListener('change', () => {
                var targetGroupClass = groupSelect.getAttribute('data-target-group') + '-checkbox';
                var checkboxes = Array.prototype.slice.call(document.querySelectorAll('.' + targetGroupClass));
                var want = groupSelect.checked;
                var nStages = getQuestionnaireStageCount();
                checkboxes.forEach((box) => {
                    var qid = box.value;
                    if (nStages <= 1) {
                        mapSet(isQuestionSelected, qid, want);
                        if (want) {
                            mapSet(questionStages, qid, 1);
                        } else {
                            mapDelete(questionStages, qid);
                        }
                        box.checked = want;
                    } else {
                        if (want) {
                            mapSet(isQuestionSelected, qid, true);
                            mapSet(questionStages, qid, activeQuestionnaireEditorStage);
                        } else if (mapIsTrue(isQuestionSelected, qid) &&
                            parseInt(mapGet(questionStages, qid, 1), 10) === activeQuestionnaireEditorStage) {
                            mapSet(isQuestionSelected, qid, false);
                            mapDelete(questionStages, qid);
                        }
                        syncCheckboxVisualFor(box);
                    }
                });
                renderRows();
            });

            var targetGroupClass = groupSelect.getAttribute('data-target-group') + '-checkbox';
            var targetBoxes = Array.prototype.slice.call(document.querySelectorAll('.' + targetGroupClass));
            if (targetBoxes.length) {
                groupSelect.checked = targetBoxes.every((box) => { return box.checked; });
            }
        });

        questionCheckboxes.forEach((cb) => {
            var qid = cb.value;
            var initial = parseFloat(cb.getAttribute('data-initial-weight'));
            if (!Number.isNaN(initial)) {
                mapSet(questionWeights, qid, clamp(initial, 0, 100));
            }
            if (cb.checked) {
                mapSet(isQuestionSelected, qid, true);
                var initStage = parseInt(cb.getAttribute('data-initial-stage'), 10);
                var cap = getQuestionnaireStageCount();
                var st = (!Number.isNaN(initStage) && initStage > 0) ? initStage : 1;
                mapSet(questionStages, qid, Math.min(cap, Math.max(1, st)));
            }
            cb.addEventListener('change', () => {
                var nStages = getQuestionnaireStageCount();
                var want = cb.checked;
                if (nStages <= 1) {
                    mapSet(isQuestionSelected, qid, want);
                    if (want) {
                        mapSet(questionStages, qid, 1);
                    } else {
                        mapDelete(questionStages, qid);
                    }
                } else {
                    if (want) {
                        mapSet(isQuestionSelected, qid, true);
                        mapSet(questionStages, qid, activeQuestionnaireEditorStage);
                    } else if (mapIsTrue(isQuestionSelected, qid) &&
                        parseInt(mapGet(questionStages, qid, 1), 10) === activeQuestionnaireEditorStage) {
                        mapSet(isQuestionSelected, qid, false);
                        mapDelete(questionStages, qid);
                    }
                    syncCheckboxVisualFor(cb);
                }
                var groupClass = Array.prototype.slice.call(cb.classList).find((cls) => {
                    return cls.indexOf('group_') === 0 && cls.indexOf('-checkbox') > -1;
                });
                if (groupClass) {
                    var groupId = groupClass.replace('-checkbox', '');
                    var groupToggle = document.querySelector('.group-select-all[data-target-group="' + CSS.escape(groupId) + '"]');
                    if (groupToggle) {
                        var groupBoxes = Array.prototype.slice.call(document.querySelectorAll('.' + groupClass));
                        groupToggle.checked = groupBoxes.every((box) => { return box.checked; });
                    }
                }
                renderRows();
            });
        });

        syncAllCheckboxVisuals();
        syncGroupSelectHeaders();

        if (redistributeButton) {
            redistributeButton.addEventListener('click', () => {
                var selected = selectedCheckboxes();
                if (!selected.length) {
                    return;
                }

                redistributeUnlockedEvenly(selected);
                syncWeightUi(selected);
            });
        }

        form.addEventListener('submit', () => {
            rebuildSelectedQuestionsHiddenInputs();
            var selected = selectedCheckboxes();
            selected.forEach((cb) => { ensureWeight(cb.value); });
            updatePayload(selected);
            updateStagesPayload(selected);
        });

        function syncHasSecondaryFromStageCount() {
            if (!secondaryStageToggleEl || !stageCountInput) {
                return;
            }
            secondaryStageToggleEl.checked = readStageCountFromInput() > 1;
        }

        function applyHasSecondaryCheckbox(checked) {
            if (!stageCountInput) {
                return;
            }
            var n = readStageCountFromInput();
            if (checked) {
                if (n < 2) {
                    stageCountInput.value = '2';
                }
            } else {
                stageCountInput.value = '1';
            }
            updateStageCountSectionVisibility();
            clampActiveEditorStage(getQuestionnaireStageCount());
            refreshQuestionnaireStageUi();
            renderRows();
        }

        if (secondaryStageToggleEl && stageCountInput) {
            secondaryStageToggleEl.addEventListener('change', () => {
                applyHasSecondaryCheckbox(secondaryStageToggleEl.checked);
            });
        }

        if (stageCountInput) {
            stageCountInput.addEventListener('change', () => {
                syncHasSecondaryFromStageCount();
                updateStageCountSectionVisibility();
                clampActiveEditorStage(getQuestionnaireStageCount());
                refreshQuestionnaireStageUi();
                renderRows();
            });
            stageCountInput.addEventListener('input', () => {
                syncHasSecondaryFromStageCount();
                updateStageCountSectionVisibility();
                clampActiveEditorStage(getQuestionnaireStageCount());
                refreshQuestionnaireStageUi();
            });
        }

        syncHasSecondaryFromStageCount();
        updateStageCountSectionVisibility();
        renderRows();

    function applyTemplateItems(template) {
        if (!template || !template.questions || !template.questions.length) {
            return;
        }
        var desiredStageCount = parseInt(template.stageCount, 10);
        if (!Number.isNaN(desiredStageCount) && desiredStageCount > getQuestionnaireStageCount()) {
            if (secondaryStageToggleEl) {
                secondaryStageToggleEl.checked = desiredStageCount > 1;
            }
            if (stageCountInput) {
                stageCountInput.value = String(desiredStageCount);
            }
            updateStageCountSectionVisibility();
            clampActiveEditorStage(getQuestionnaireStageCount());
        }
        template.questions.forEach((item) => {
            var qid = String(item.questionId);
            if (mapIsTrue(isQuestionSelected, qid)) {
                return;
            }
            mapSet(isQuestionSelected, qid, true);
            var weight = parseFloat(item.weight);
            mapSet(questionWeights, qid, clamp(Number.isNaN(weight) ? 0 : Math.round(weight), 0, 100));
            var stage = parseInt(item.stageNumber, 10);
            mapSet(questionStages, qid, Number.isNaN(stage) || stage < 1 ? 1 : stage);
        });
        syncAllCheckboxVisuals();
        syncGroupSelectHeaders();
        renderRows();
    }

    var api = { applyTemplateItems: applyTemplateItems };
    window.questionnaireAssignmentEditor = api;
    return api;
}

window.initQuestionnaireAssignmentEditor = initQuestionnaireAssignmentEditor;
