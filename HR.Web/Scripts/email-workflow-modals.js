(function (window, $) {
    'use strict';

    if (!$) {
        return;
    }

    var modalSwapInProgress = false;

    function syncModalBackdrops() {
        if (modalSwapInProgress) {
            return;
        }

        var shown = $('.modal.show');
        var backdropCount = $('.modal-backdrop').length;

        if (shown.length === 0) {
            if (backdropCount > 0) {
                $('.modal-backdrop').remove();
            }
            $('body').removeClass('modal-open');
            $('body').css({ 'padding-right': '', overflow: '' });
            return;
        }

        if (backdropCount > shown.length) {
            $('.modal-backdrop').slice(shown.length).remove();
        }
    }

    function swapModal(fromSelector, toSelector) {
        var $from = fromSelector ? $(fromSelector) : $();
        var $to = $(toSelector);
        if (!$to.length) {
            return;
        }

        ensureModalOnBody($to[0]);
        if ($from.length) {
            ensureModalOnBody($from[0]);
        }

        if ($from.length && $from.hasClass('show')) {
            modalSwapInProgress = true;
            $from.one('hidden.bs.modal', function () {
                $to.modal('show');
            });
            $from.modal('hide');
            return;
        }

        syncModalBackdrops();
        $to.modal('show');
    }

    function withCallbackTimeout(callback, timeoutMs) {
        var finished = false;
        var timer = null;

        function finish() {
            if (finished) {
                return;
            }
            finished = true;
            if (timer) {
                clearTimeout(timer);
                timer = null;
            }
            if (callback) {
                callback();
            }
        }

        if (timeoutMs > 0) {
            timer = setTimeout(finish, timeoutMs);
        }

        return finish;
    }

    function ensureModalOnBody(modalElement) {
        var $modal = $(modalElement);
        if ($modal.length && !$modal.parent().is('body')) {
            $modal.appendTo('body');
        }
    }

    $(function () {
        $('.email-workflow-modal').each(function () {
            ensureModalOnBody(this);
        });

        $(document).on('show.bs.modal', '.email-workflow-modal', function () {
            ensureModalOnBody(this);
        });

        $(document).on('shown.bs.modal', '.email-workflow-modal', function () {
            modalSwapInProgress = false;
            window.setTimeout(syncModalBackdrops, 0);
        });

        $(document).on('hidden.bs.modal', '.email-workflow-modal', function () {
            window.setTimeout(function () {
                if (modalSwapInProgress) {
                    return;
                }
                syncModalBackdrops();
            }, 0);
        });
    });

    window.HrEmailWorkflow = {
        swapModal: swapModal,
        syncModalBackdrops: syncModalBackdrops,
        withCallbackTimeout: withCallbackTimeout
    };
})(window, window.jQuery);