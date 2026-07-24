from app.application.ports import StemSeparatorPort


class StemSeparatorRegistry:
    def __init__(self) -> None:
        self._providers: dict[str, StemSeparatorPort] = {}

    def register(self, provider_name: str, provider: StemSeparatorPort) -> None:
        self._providers[self._normalize(provider_name)] = provider

    def get(self, provider_name: str) -> StemSeparatorPort:
        provider = self._providers.get(self._normalize(provider_name))
        if provider is None:
            raise ValueError(f"Unsupported stem separator provider: {provider_name}")

        return provider

    @staticmethod
    def _normalize(provider_name: str) -> str:
        return provider_name.strip().lower()
