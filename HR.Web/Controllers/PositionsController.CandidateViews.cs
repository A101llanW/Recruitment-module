using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Helpers;
using HR.Web.Models;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class PositionsController
    {
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult RecordCandidateView(int positionId)
        {
            var user = FindAuthenticatedCandidate();
            if (user == null)
            {
                return Json(new { success = false });
            }

            var position = _uow.Positions.Get(positionId);
            if (position == null)
            {
                return HttpNotFound();
            }

            RecordCandidatePositionView(position, user);
            return Json(new { success = true });
        }

        private void ApplyRecentlyViewedFlags(IList<Position> positions)
        {
            var actions = ViewBag.CandidateActionsByPositionId as Dictionary<int, PositionCandidateActionViewModel>;
            if (actions == null || actions.Count == 0 || positions == null)
            {
                return;
            }

            var user = FindAuthenticatedCandidate();
            if (user == null)
            {
                return;
            }

            var positionIds = actions.Keys.ToList();
            var views = _uow.Context.PositionViews
                .AsNoTracking()
                .Where(v => v.UserId == user.Id && positionIds.Contains(v.PositionId))
                .ToList();
            var viewsByPositionId = views.ToDictionary(v => v.PositionId);

            foreach (var position in positions)
            {
                if (position == null)
                {
                    continue;
                }

                PositionCandidateActionViewModel action;
                PositionView view;
                if (!actions.TryGetValue(position.Id, out action) || !viewsByPositionId.TryGetValue(position.Id, out view))
                {
                    continue;
                }

                action.ShowRecentlyViewed = PositionViewTagHelper.ShouldShowRecentlyViewed(
                    view,
                    user.SuccessfulLoginCount,
                    position.IsOpen,
                    action.HasApplied);
            }
        }

        private void RecordCandidatePositionView(Position position)
        {
            RecordCandidatePositionView(position, FindAuthenticatedCandidate());
        }

        private void RecordCandidatePositionView(Position position, User user)
        {
            if (position == null || user == null)
            {
                return;
            }

            var existing = _uow.Context.PositionViews
                .FirstOrDefault(v => v.UserId == user.Id && v.PositionId == position.Id);
            if (existing == null)
            {
                _uow.Context.PositionViews.Add(new PositionView
                {
                    UserId = user.Id,
                    PositionId = position.Id,
                    ViewedAtUtc = DateTime.UtcNow,
                    LoginCountAtView = user.SuccessfulLoginCount,
                    IsOpenAtView = position.IsOpen
                });
            }
            else
            {
                existing.ViewedAtUtc = DateTime.UtcNow;
                existing.LoginCountAtView = user.SuccessfulLoginCount;
                existing.IsOpenAtView = position.IsOpen;
            }

            _uow.Complete();
        }

        private User FindAuthenticatedCandidate()
        {
            if (!Request.IsAuthenticated || User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return null;
            }

            if (User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || _tenantService.IsSuperAdmin())
            {
                return null;
            }

            var name = User.Identity.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var normalized = name.Trim();
            return _uow.Context.Users.FirstOrDefault(u => u.UserName == normalized);
        }
    }
}
