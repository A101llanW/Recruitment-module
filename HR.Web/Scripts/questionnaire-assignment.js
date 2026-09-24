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

function parseInitialQuestionStages(cb) {
    var multi = cb.getAttribute('data-initial-stages');
    if (multi) {
        return multi.split(',').map((token) => {
            return parseInt(token.trim(), 10);
        }).filter((stage) => {
            return !Number.isNaN(stage) && stage > 0;
        });
    }

    var single = parseInt(cb.getAttribute('data-initial-stage'), 10);
    if (!Number.isNaN(single) && single > 0) {
        return [single];
    }

    return [1];
}

function getQuestionStageSet(map, questionId) {
    if (!map.has(questionId)) {
        map.set(questionId, new Set());
    }
    return map.get(questionId);
}

function isQuestionAssignedToStage(map, questionId, stage) {
    return map.has(questionId) && map.get(questionId).has(stage);
}

function isQuestionAssignedToAnyStage(map, questionId) {
    return map.has(questionId) && map.get(questionId).size > 0;
}

function assignQuestionToStage(map, questionId, stage) {
    getQuestionStageSet(map, questionId).add(stage);
}

function unassignQuestionFromStage(map, questionId, stage) {
    if (!map.has(questionId)) {
        return;
    }

    map.get(questionId).delete(stage);
    if (map.get(questionId).size === 0) {
        map.delete(questionId);
    }
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

function readTemplateField(item, camelKey, pascalKey) {
    if (!item) {
        return undefined;
    }
    if (item[camelKey] !== undefined && item[camelKey] !== null) {
        return item[camelKey];
    }
    return item[pascalKey];
}

function normalizeTemplatePayload(template) {
    if (!template) {
        return { stageCount: 1, questions: [] };
    }

    var rawQuestions = template.questions || template.Questions || [];
    if (!Array.isArray(rawQuestions)) {
        rawQuestions = [];
    }

    var questions = [];
    rawQuestions.forEach((item) => {
        var questionId = readTemplateField(item, 'questionId', 'QuestionId');
        if (questionId === undefined || questionId === null || questionId === '') {
            return;
        }

        questions.push({
            questionId: questionId,
            weight: readTemplateField(item, 'weight', 'Weight'),
            stageNumber: readTemplateField(item, 'stageNumber', 'StageNumber')
        });
    });

    var stageCount = readTemplateField(template, 'stageCount', 'StageCount');
    return {
        stageCount: stageCount,
        questions: questions
    };
}

function buildQuestionCheckboxLookup(checkboxes) {
    var lookup = new Map();
    checkboxes.forEach((cb) => {
        lookup.set(String(cb.value), cb);
    });
    return lookup;
}

function initQuestionnaireAssignmentEditor(options) {
    options = options || {};
        var formIdOption = options.formId;
        var stageCountInputIdOption = options.stageCountInputId;
        var multiStageToggleFieldIdOption = options.multiStageToggleFieldId;
        var stageCountSectionIdOption = options.stageCountSectionId;
        var lockedStageList = Array.isArray(options.lockedStages) ? options.lockedStages : [];
        var lockedStages = new Set(lockedStageList.map(function (stage) {
            return parseInt(stage, 10);
        }).filter(function (stage) {
            return !Number.isNaN(stage) && stage > 0;
        }));
        var minStageCount = parseIntSafe(options.minStageCount, 1);
        if (minStageCount < 1) {
            minStageCount = 1;
        }
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
        var questionCheckboxById = buildQuestionCheckboxLookup(questionCheckboxes);
        var selectedQuestionsHiddenContainer = document.getElementById('selectedQuestionsHiddenContainer');
        var questionWeights = new Map();
        var questionLocks = new Map();
        var questionStages = new Map();
        var activeQuestionnaireEditorStage = 1;

        function isStageLocked(stage) {
            return lockedStages.has(stage);
        }

        function isQuestionOnLockedStage(questionId) {
            if (!questionStages.has(questionId)) {
                return false;
            }

            var stageSet = questionStages.get(questionId);
            if (!stageSet || !stageSet.size) {
                return false;
            }

            var locked = false;
            stageSet.forEach(function (stage) {
                if (isStageLocked(stage)) {
                    locked = true;
                }
            });
            return locked;
        }

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
                    if (isStageLocked(stageNum)) {
                        btn.className += ' questionnaire-stage-switcher-btn--locked';
                        btn.title = 'Stage locked because candidates have submitted answers';
                    }
                    btn.setAttribute('role', 'tab');
                    btn.setAttribute('aria-selected', stageNum === activeQuestionnaireEditorStage ? 'true' : 'false');
                    btn.textContent = isStageLocked(stageNum) ? ('Stage ' + stageNum + ' (locked)') : ('Stage ' + stageNum);
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
                return isQuestionAssignedToAnyStage(questionStages, cb.value);
            });
        }

        function syncCheckboxVisualFor(cb) {
            var qid = cb.value;
            var n = getQuestionnaireStageCount();
            if (n <= 1) {
                cb.checked = isQuestionAssignedToAnyStage(questionStages, qid);
                cb.disabled = isStageLocked(1);
                return;
            }
            cb.checked = isQuestionAssignedToStage(questionStages, qid, activeQuestionnaireEditorStage);
            cb.disabled = isStageLocked(activeQuestionnaireEditorStage);
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
                if (!isQuestionAssignedToAnyStage(questionStages, qid)) {
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
            var pairs = [];
            selected.forEach((cb) => {
                var qid = cb.value;
                var stageSet = questionStages.get(qid);
                if (!stageSet || !stageSet.size) {
                    return;
                }

                var stageList = Array.from(stageSet)
                    .map((stage) => {
                        return parseInt(stage, 10);
                    })
                    .filter((stage) => {
                        return !Number.isNaN(stage) && stage >= 1;
                    })
                    .map((stage) => {
                        return Math.min(maxS, stage);
                    })
                    .sort((a, b) => {
                        return a - b;
                    });

                if (!stageList.length) {
                    stageList.push(1);
                }

                questionStages.set(qid, new Set(stageList));
                pairs.push(qid + '=' + stageList.join(','));
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

                var locked = mapIsTrue(questionLocks, questionId) || isQuestionOnLockedStage(questionId);
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
                if (!isQuestionAssignedToAnyStage(questionStages, qid)) {
                    assignQuestionToStage(questionStages, qid, 1);
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
                if ((nStages <= 1 && isStageLocked(1)) || (nStages > 1 && isStageLocked(activeQuestionnaireEditorStage))) {
                    syncGroupSelectHeaders();
                    return;
                }
                checkboxes.forEach((box) => {
                    var qid = box.value;
                    if (nStages <= 1) {
                        if (want) {
                            questionStages.set(qid, new Set([1]));
                        } else {
                            mapDelete(questionStages, qid);
                        }
                        box.checked = want;
                    } else if (want) {
                        assignQuestionToStage(questionStages, qid, activeQuestionnaireEditorStage);
                        syncCheckboxVisualFor(box);
                    } else if (isQuestionAssignedToStage(questionStages, qid, activeQuestionnaireEditorStage)) {
                        unassignQuestionFromStage(questionStages, qid, activeQuestionnaireEditorStage);
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
                var cap = getQuestionnaireStageCount();
                parseInitialQuestionStages(cb).forEach((stage) => {
                    assignQuestionToStage(questionStages, qid, Math.min(cap, Math.max(1, stage)));
                });
            }
            cb.addEventListener('change', () => {
                var nStages = getQuestionnaireStageCount();
                var want = cb.checked;
                if (nStages <= 1 && isStageLocked(1)) {
                    syncCheckboxVisualFor(cb);
                    return;
                }
                if (nStages > 1 && isStageLocked(activeQuestionnaireEditorStage)) {
                    syncCheckboxVisualFor(cb);
                    return;
                }
                if (nStages <= 1) {
                    if (want) {
                        questionStages.set(qid, new Set([1]));
                    } else {
                        mapDelete(questionStages, qid);
                    }
                } else if (want) {
                    assignQuestionToStage(questionStages, qid, activeQuestionnaireEditorStage);
                    syncCheckboxVisualFor(cb);
                } else if (isQuestionAssignedToStage(questionStages, qid, activeQuestionnaireEditorStage)) {
                    unassignQuestionFromStage(questionStages, qid, activeQuestionnaireEditorStage);
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
            if (minStageCount > 1 && !checked) {
                secondaryStageToggleEl.checked = true;
                checked = true;
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
            stageCountInput.min = String(minStageCount);
            stageCountInput.addEventListener('change', () => {
                if (readStageCountFromInput() < minStageCount) {
                    stageCountInput.value = String(minStageCount);
                }
                syncHasSecondaryFromStageCount();
                updateStageCountSectionVisibility();
                clampActiveEditorStage(getQuestionnaireStageCount());
                refreshQuestionnaireStageUi();
                renderRows();
            });
            stageCountInput.addEventListener('input', () => {
                if (readStageCountFromInput() < minStageCount) {
                    stageCountInput.value = String(minStageCount);
                }
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
        var payload = normalizeTemplatePayload(template);
        if (!payload.questions.length) {
            return { appliedCount: 0, missingCount: 0 };
        }

        var desiredStageCount = parseInt(payload.stageCount, 10);
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

        var maxStage = getQuestionnaireStageCount();
        var appliedIds = new Set();
        var missingCount = 0;

        payload.questions.forEach((item) => {
            var qid = String(item.questionId);
            if (!questionCheckboxById.has(qid)) {
                missingCount += 1;
                return;
            }

            appliedIds.add(qid);
            var weight = parseFloat(item.weight);
            mapSet(questionWeights, qid, clamp(Number.isNaN(weight) ? 0 : Math.round(weight), 0, 100));
            var stage = parseInt(item.stageNumber, 10);
            if (Number.isNaN(stage) || stage < 1) {
                stage = 1;
            }
            if (isStageLocked(stage)) {
                return;
            }
            assignQuestionToStage(questionStages, qid, Math.min(maxStage, stage));
        });

        syncAllCheckboxVisuals();
        syncGroupSelectHeaders();
        renderRows();
        rebuildSelectedQuestionsHiddenInputs();

        return {
            appliedCount: appliedIds.size,
            missingCount: missingCount
        };
    }

    var api = { applyTemplateItems: applyTemplateItems };
    window.questionnaireAssignmentEditor = api;
    return api;
}

window.initQuestionnaireAssignmentEditor = initQuestionnaireAssignmentEditor;
