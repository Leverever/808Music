class BackendCallbackError(RuntimeError):
    def __init__(self, status_code: int, response_text: str) -> None:
        self.status_code = status_code
        self.response_text = response_text
        super().__init__(f"Backend callback failed with HTTP {status_code}: {response_text}")

    @property
    def is_retryable(self) -> bool:
        return self.status_code >= 500 or self.status_code == 429
