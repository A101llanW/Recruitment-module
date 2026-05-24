// Shared questionnaire assignment editor
function initQuestionnaireAssignmentEditor(options) {
    options = options || {};
        var form = document.getElementById(options.formId) || document.getElementById('positionCreateForm') || document.getElementById('positionEditForm') || document.getElementById('templateEditForm');
        if (!form) return;

        var payloadInput = document.getElementById('questionWeightsPayload');
        var stagesPayloadInput = document.getElementById('questionStagesPayload');
        var stageCountInput = document.getElementById(options.stageCountInputId || 'questionnaireStageCountInput');
        var hasSecondaryCheckbox = document.getElementById(options.hasSecondaryStageCheckboxId || 'hasSecondaryStageCheckbox');
        var stageCountSection = document.getElementById(options.stageCountSectionId || 'questionnaireStageCountSection');
        var legendRow = document.getElementById('questionnaireStageLegendRow');
        var legendEl = document.getElementById('questionnaireStageLegend');
        var budgetText = document.getElementById('questionWeightBudgetText');
        var budgetBar = document.getElementById('questionWeightBudgetBar');
        var redistributeButton = document.getElementById('questionWeightRedistributeBtn');
        var weightRows = document.getElementById('questionWeightRows');
        var weightEmpty = document.getElementById('questionWeightEmpty');
        var questionCheckboxes = Array.prototype.slice.call(document.querySelectorAll('.question-checkbox'));
        var selectedQuestionsHiddenContainer = document.getElementById('selectedQuestionsHiddenContainer');
        var questionWeights = {};
        var questionLocks = {};
        var questionStages = {};
        var isQuestionSelected = {};
        var activeQuestionnaireEditorStage = 1;

        function clamp(v, min, max) {
            return Math.max(min, Math.min(max, v));
        }

        function readStageCountFromInput() {
            if (!stageCountInput) {
                return 1;
            }
            var n = parseInt(stageCountInput.value, 10);
            if (isNaN(n) || n < 1) {
                return 1;
            }
            if (n > 10) {
                return 10;
            }
            return n;
        }

        function getQuestionnaireStageCount() {
            if (hasSecondaryCheckbox && !hasSecondaryCheckbox.checked) {
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
            legendEl.innerHTML = '';
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
                    btn.addEventListener('click', function () {
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
            return questionCheckboxes.filter(function (cb) {
                return isQuestionSelected[cb.value] === true;
            });
        }

        function syncCheckboxVisualFor(cb) {
            var qid = cb.value;
            var n = getQuestionnaireStageCount();
            if (n <= 1) {
                cb.checked = isQuestionSelected[qid] === true;
                return;
            }
            cb.checked = isQuestionSelected[qid] === true &&
                parseInt(questionStages[qid], 10) === activeQuestionnaireEditorStage;
        }

        function syncAllCheckboxVisuals() {
            questionCheckboxes.forEach(syncCheckboxVisualFor);
        }

        function syncGroupSelectHeaders() {
            Array.prototype.slice.call(document.querySelectorAll('.group-select-all')).forEach(function (groupSelect) {
                var targetGroupClass = groupSelect.getAttribute('data-target-group') + '-checkbox';
                var targetBoxes = Array.prototype.slice.call(document.querySelectorAll('.' + targetGroupClass));
                if (targetBoxes.length) {
                    groupSelect.checked = targetBoxes.every(function (box) {
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
            if (stageCountSection && hasSecondaryCheckbox) {
                if (hasSecondaryCheckbox.checked) {
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
            selectedQuestionsHiddenContainer.innerHTML = '';
            questionCheckboxes.forEach(function (cb) {
                var qid = cb.value;
                if (isQuestionSelected[qid] !== true) {
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
            var pairs = selected.map(function (cb) {
                var qid = cb.value;
                var st = parseInt(questionStages[qid], 10);
                if (isNaN(st) || st < 1) {
                    st = 1;
                }
                if (st > maxS) {
                    st = maxS;
                }
                questionStages[qid] = st;
                return qid + '=' + st;
            });
            stagesPayloadInput.value = pairs.join(';');
        }

        function parseIntSafe(raw, fallback) {
            var parsed = parseInt(raw, 10);
            return isNaN(parsed) ? fallback : parsed;
        }

        function selectedIds(selected) {
            return selected.map(function (cb) { return cb.value; });
        }

        function ensureWeight(questionId) {
            var current = parseIntSafe(questionWeights[questionId], NaN);
            if (isNaN(current)) {
                current = 0;
            }
            questionWeights[questionId] = clamp(current, 0, 100);
        }

        function sumByIds(questionIds) {
            return questionIds.reduce(function (sum, questionId) {
                ensureWeight(questionId);
                return sum + questionWeights[questionId];
            }, 0);
        }

        function normalizeIntegerDistribution(questionIds, targetTotal, basisAccessor) {
            var sanitizedTarget = clamp(parseIntSafe(targetTotal, 0), 0, 100);
            if (!questionIds.length) {
                return {};
            }

            if (questionIds.length === 1) {
                var single = {};
                single[questionIds[0]] = sanitizedTarget;
                return single;
            }

            var basis = questionIds.map(function (questionId) {
                var raw = basisAccessor(questionId);
                var safe = clamp(parseIntSafe(raw, 0), 0, 100);
                return { id: questionId, weight: safe };
            });

            var totalBasis = basis.reduce(function (sum, item) { return sum + item.weight; }, 0);
            if (totalBasis <= 0) {
                var evenBase = Math.floor(sanitizedTarget / questionIds.length);
                var evenRemainder = sanitizedTarget - (evenBase * questionIds.length);
                var evenResult = {};
                questionIds.forEach(function (questionId, index) {
                    evenResult[questionId] = evenBase + (index < evenRemainder ? 1 : 0);
                });
                return evenResult;
            }

            var allocation = basis.map(function (item, index) {
                var exact = (item.weight / totalBasis) * sanitizedTarget;
                var floored = Math.floor(exact);
                return {
                    id: item.id,
                    index: index,
                    value: floored,
                    fraction: exact - floored
                };
            });

            var assigned = allocation.reduce(function (sum, item) { return sum + item.value; }, 0);
            var remainder = sanitizedTarget - assigned;
            allocation.sort(function (a, b) { return b.fraction - a.fraction; });
            for (var i = 0; i < remainder; i++) {
                allocation[i % allocation.length].value += 1;
            }
            allocation.sort(function (a, b) { return a.index - b.index; });

            var result = {};
            allocation.forEach(function (item) {
                result[item.id] = clamp(item.value, 0, 100);
            });
            return result;
        }

        function updatePayload(selected) {
            var pairs = selected.map(function (cb) {
                return cb.value + '=' + (questionWeights[cb.value] || 0);
            });
            payloadInput.value = pairs.join(';');
        }

        function updateBudgetUi(selected) {
            var total = selected.reduce(function (sum, cb) {
                ensureWeight(cb.value);
                return sum + questionWeights[cb.value];
            }, 0);
            var boundedTotal = clamp(total, 0, 100);
            budgetText.textContent = total + ' / 100 allocated';
            budgetBar.style.width = boundedTotal + '%';
        }

        function updateRedistributeButtonState(selected) {
            if (!redistributeButton) {
                return;
            }

            var unlockedCount = selected.filter(function (cb) {
                return questionLocks[cb.value] !== true;
            }).length;

            redistributeButton.disabled = !selected.length || unlockedCount === 0;
        }

        function redistributeUnlockedEvenly(selected) {
            if (!selected.length) {
                return;
            }

            var ids = selectedIds(selected);
            ids.forEach(ensureWeight);

            var lockedIds = ids.filter(function (questionId) { return questionLocks[questionId] === true; });
            var unlockedIds = ids.filter(function (questionId) { return questionLocks[questionId] !== true; });
            if (!unlockedIds.length) {
                return;
            }

            var lockedTotal = sumByIds(lockedIds);
            if (lockedTotal > 100) {
                var compressedLocked = normalizeIntegerDistribution(lockedIds, 100, function (questionId) { return questionWeights[questionId]; });
                lockedIds.forEach(function (questionId) { questionWeights[questionId] = compressedLocked[questionId]; });
                lockedTotal = 100;
            }

            var unlockedTarget = clamp(100 - lockedTotal, 0, 100);
            var base = Math.floor(unlockedTarget / unlockedIds.length);
            var remainder = unlockedTarget - (base * unlockedIds.length);
            unlockedIds.forEach(function (questionId, index) {
                questionWeights[questionId] = base + (index < remainder ? 1 : 0);
            });
        }

        function syncWeightUi(selected) {
            if (!selected.length) {
                weightEmpty.style.display = '';
                budgetText.textContent = '0 / 100 allocated';
                budgetBar.style.width = '0%';
                payloadInput.value = '';
                if (stagesPayloadInput) {
                    stagesPayloadInput.value = '';
                }
                updateRedistributeButtonState(selected);
                refreshQuestionnaireStageUi();
                return;
            }

            weightEmpty.style.display = 'none';
            selected.forEach(function (cb) {
                var questionId = cb.value;
                ensureWeight(questionId);
                var row = weightRows.querySelector('.question-weight-row[data-question-id="' + questionId + '"]');
                if (!row) {
                    return;
                }

                var locked = questionLocks[questionId] === true;
                row.classList.toggle('locked', locked);

                var slider = row.querySelector('.question-weight-slider');
                var number = row.querySelector('.question-weight-number');
                var value = row.querySelector('.question-weight-value');
                var icon = row.querySelector('.question-weight-lock i');
                var current = questionWeights[questionId];

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

            if (questionLocks[questionId] === true) {
                syncWeightUi(selected);
                return;
            }

            var ids = selectedIds(selected);
            ids.forEach(ensureWeight);

            function sumWeightsByIds(questionIds) {
                return questionIds.reduce(function (sum, id) {
                    ensureWeight(id);
                    return sum + (questionWeights[id] || 0);
                }, 0);
            }

            function reduceUnlockedPool(poolIds, reductionNeeded) {
                if (!poolIds.length || reductionNeeded <= 0) {
                    return;
                }

                var pool = poolIds
                    .map(function (id, index) {
                        ensureWeight(id);
                        return { id: id, index: index, weight: questionWeights[id] || 0 };
                    })
                    .filter(function (item) { return item.weight > 0; });

                if (!pool.length) {
                    return;
                }

                var poolTotal = pool.reduce(function (sum, item) { return sum + item.weight; }, 0);
                if (poolTotal <= 0) {
                    return;
                }

                var reductions = pool.map(function (item) {
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

                var assigned = reductions.reduce(function (sum, item) { return sum + item.value; }, 0);
                var remainder = reductionNeeded - assigned;
                reductions.sort(function (a, b) { return b.fraction - a.fraction; });

                for (var i = 0; i < remainder; i++) {
                    var pick = reductions[i % reductions.length];
                    if (pick.value < pick.max) {
                        pick.value += 1;
                    }
                }

                reductions.sort(function (a, b) { return a.index - b.index; });
                reductions.forEach(function (item) {
                    questionWeights[item.id] = clamp((questionWeights[item.id] || 0) - item.value, 0, 100);
                });
            }

            var lockedIds = ids.filter(function (id) { return questionLocks[id] === true; });
            var lockedTotal = sumWeightsByIds(lockedIds);
            var maxForThis = clamp(100 - lockedTotal, 0, 100);

            var current = questionWeights[questionId] || 0;
            var desired = clamp(parseIntSafe(rawValue, 0), 0, maxForThis);

            if (desired <= current) {
                questionWeights[questionId] = desired;
                syncWeightUi(selected);
                return;
            }

            questionWeights[questionId] = desired;
            var otherUnlockedIds = ids.filter(function (id) { return id !== questionId && questionLocks[id] !== true; });
            var otherUnlockedTotal = sumWeightsByIds(otherUnlockedIds);
            var totalAfter = lockedTotal + desired + otherUnlockedTotal;
            var reductionNeeded = Math.max(0, totalAfter - 100);
            reduceUnlockedPool(otherUnlockedIds, reductionNeeded);

            syncWeightUi(selected);
        }

        function toggleLock(questionId) {
            questionLocks[questionId] = !(questionLocks[questionId] === true);
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
            lockButton.innerHTML = "<i class='fas fa-lock-open'></i>";
            lockButton.addEventListener('click', function () {
                toggleLock(questionId);
            });

            var slider = document.createElement('input');
            slider.type = 'range';
            slider.min = '0';
            slider.max = '100';
            slider.step = '1';
            slider.className = 'flex-grow-1 question-weight-slider';
            slider.value = questionWeights[questionId];
            slider.addEventListener('input', function () {
                setFocusedWeight(questionId, slider.value);
            });
            slider.addEventListener('change', function () {
                setFocusedWeight(questionId, slider.value);
            });

            var number = document.createElement('input');
            number.type = 'number';
            number.min = '0';
            number.max = '100';
            number.step = '1';
            number.className = 'form-control form-control-sm ml-2 question-weight-number';
            number.style.maxWidth = '72px';
            number.value = questionWeights[questionId];
            number.addEventListener('input', function () {
                setFocusedWeight(questionId, number.value);
            });
            number.addEventListener('change', function () {
                setFocusedWeight(questionId, number.value);
            });

            var value = document.createElement('span');
            value.className = 'question-weight-value ml-2';
            value.textContent = questionWeights[questionId] + ' pts';

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
            weightRows.innerHTML = '';

            if (!selected.length) {
                syncWeightUi(selected);
                return;
            }

            selected.forEach(function (cb) {
                ensureWeight(cb.value);
                var qid = cb.value;
                if (questionStages[qid] == null || isNaN(parseInt(questionStages[qid], 10))) {
                    questionStages[qid] = 1;
                }
            });

            var ids = selectedIds(selected);
            var total = ids.reduce(function (sum, id) { return sum + (questionWeights[id] || 0); }, 0);
            if (total <= 0) {
                if (ids.length === 1) {
                    questionWeights[ids[0]] = 100;
                } else {
                    var base = Math.floor(100 / ids.length);
                    var rem = 100 - (base * ids.length);
                    ids.forEach(function (id, idx) {
                        questionWeights[id] = base + (idx < rem ? 1 : 0);
                    });
                }
            }

            selected.forEach(function (cb) {
                weightRows.appendChild(createWeightRow(cb));
            });

            syncWeightUi(selected);
        }

        var groupSelects = Array.prototype.slice.call(document.querySelectorAll('.group-select-all'));
        groupSelects.forEach(function (groupSelect) {
            groupSelect.addEventListener('change', function () {
                var targetGroupClass = groupSelect.getAttribute('data-target-group') + '-checkbox';
                var checkboxes = Array.prototype.slice.call(document.querySelectorAll('.' + targetGroupClass));
                var want = groupSelect.checked;
                var nStages = getQuestionnaireStageCount();
                checkboxes.forEach(function (box) {
                    var qid = box.value;
                    if (nStages <= 1) {
                        isQuestionSelected[qid] = want;
                        if (want) {
                            questionStages[qid] = 1;
                        } else {
                            delete questionStages[qid];
                        }
                        box.checked = want;
                    } else {
                        if (want) {
                            isQuestionSelected[qid] = true;
                            questionStages[qid] = activeQuestionnaireEditorStage;
                        } else if (isQuestionSelected[qid] === true &&
                            parseInt(questionStages[qid], 10) === activeQuestionnaireEditorStage) {
                            isQuestionSelected[qid] = false;
                            delete questionStages[qid];
                        }
                        syncCheckboxVisualFor(box);
                    }
                });
                renderRows();
            });

            var targetGroupClass = groupSelect.getAttribute('data-target-group') + '-checkbox';
            var targetBoxes = Array.prototype.slice.call(document.querySelectorAll('.' + targetGroupClass));
            if (targetBoxes.length) {
                groupSelect.checked = targetBoxes.every(function (box) { return box.checked; });
            }
        });

        questionCheckboxes.forEach(function (cb) {
            var qid = cb.value;
            var initial = parseFloat(cb.getAttribute('data-initial-weight'));
            if (!isNaN(initial)) {
                questionWeights[qid] = clamp(initial, 0, 100);
            }
            if (cb.checked) {
                isQuestionSelected[qid] = true;
                var initStage = parseInt(cb.getAttribute('data-initial-stage'), 10);
                var cap = getQuestionnaireStageCount();
                var st = (!isNaN(initStage) && initStage > 0) ? initStage : 1;
                questionStages[qid] = Math.min(cap, Math.max(1, st));
            }
            cb.addEventListener('change', function () {
                var nStages = getQuestionnaireStageCount();
                var want = cb.checked;
                if (nStages <= 1) {
                    isQuestionSelected[qid] = want;
                    if (want) {
                        questionStages[qid] = 1;
                    } else {
                        delete questionStages[qid];
                    }
                } else {
                    if (want) {
                        isQuestionSelected[qid] = true;
                        questionStages[qid] = activeQuestionnaireEditorStage;
                    } else if (isQuestionSelected[qid] === true &&
                        parseInt(questionStages[qid], 10) === activeQuestionnaireEditorStage) {
                        isQuestionSelected[qid] = false;
                        delete questionStages[qid];
                    }
                    syncCheckboxVisualFor(cb);
                }
                var groupClass = Array.prototype.slice.call(cb.classList).filter(function (cls) {
                    return cls.indexOf('group_') === 0 && cls.indexOf('-checkbox') > -1;
                })[0];
                if (groupClass) {
                    var groupId = groupClass.replace('-checkbox', '');
                    var groupToggle = document.querySelector('.group-select-all[data-target-group="' + groupId + '"]');
                    if (groupToggle) {
                        var groupBoxes = Array.prototype.slice.call(document.querySelectorAll('.' + groupClass));
                        groupToggle.checked = groupBoxes.every(function (box) { return box.checked; });
                    }
                }
                renderRows();
            });
        });

        syncAllCheckboxVisuals();
        syncGroupSelectHeaders();

        if (redistributeButton) {
            redistributeButton.addEventListener('click', function () {
                var selected = selectedCheckboxes();
                if (!selected.length) {
                    return;
                }

                redistributeUnlockedEvenly(selected);
                syncWeightUi(selected);
            });
        }

        form.addEventListener('submit', function () {
            rebuildSelectedQuestionsHiddenInputs();
            var selected = selectedCheckboxes();
            selected.forEach(function (cb) { ensureWeight(cb.value); });
            updatePayload(selected);
            updateStagesPayload(selected);
        });

        function syncHasSecondaryFromStageCount() {
            if (!hasSecondaryCheckbox || !stageCountInput) {
                return;
            }
            hasSecondaryCheckbox.checked = readStageCountFromInput() > 1;
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

        if (hasSecondaryCheckbox && stageCountInput) {
            hasSecondaryCheckbox.addEventListener('change', function () {
                applyHasSecondaryCheckbox(hasSecondaryCheckbox.checked);
            });
        }

        if (stageCountInput) {
            stageCountInput.addEventListener('change', function () {
                syncHasSecondaryFromStageCount();
                updateStageCountSectionVisibility();
                clampActiveEditorStage(getQuestionnaireStageCount());
                refreshQuestionnaireStageUi();
                renderRows();
            });
            stageCountInput.addEventListener('input', function () {
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
        if (!isNaN(desiredStageCount) && desiredStageCount > getQuestionnaireStageCount()) {
            if (hasSecondaryCheckbox) {
                hasSecondaryCheckbox.checked = desiredStageCount > 1;
            }
            if (stageCountInput) {
                stageCountInput.value = String(desiredStageCount);
            }
            updateStageCountSectionVisibility();
            clampActiveEditorStage(getQuestionnaireStageCount());
        }
        template.questions.forEach(function (item) {
            var qid = String(item.questionId);
            if (isQuestionSelected[qid] === true) {
                return;
            }
            isQuestionSelected[qid] = true;
            var weight = parseFloat(item.weight);
            questionWeights[qid] = clamp(isNaN(weight) ? 0 : Math.round(weight), 0, 100);
            var stage = parseInt(item.stageNumber, 10);
            questionStages[qid] = isNaN(stage) || stage < 1 ? 1 : stage;
        });
        syncAllCheckboxVisuals();
        syncGroupSelectHeaders();
        renderRows();
    }

    var api = { applyTemplateItems: applyTemplateItems };
    window.questionnaireAssignmentEditor = api;
    return api;
}
