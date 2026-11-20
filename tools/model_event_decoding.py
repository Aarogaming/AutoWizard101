"""Recognize the D2E model's no-action boundary without hiding malformed events."""


def is_next_screen_boundary(token_ids, event_start_id, screen_id):
    """A generation ending immediately in the screen token predicts no action.

    This intentionally accepts only the two known exact forms. Other partial or
    malformed generations must still go through normal event validation.
    """
    tokens = [int(token) for token in token_ids]
    return tokens == [int(event_start_id), int(screen_id)] or tokens == [int(screen_id)]
