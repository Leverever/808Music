import {MyUserAuthService} from './my-user-auth.service';

describe('MyUserAuthService admin roles', () => {
  let service: MyUserAuthService;

  beforeEach(() => {
    jasmine.clock().install();
    localStorage.clear();
    sessionStorage.clear();
    service = new MyUserAuthService({} as any, {} as any, {} as any);
  });

  afterEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    jasmine.clock().uninstall();
  });

  it('reads the short role claim instead of trusting cached isAdmin state', () => {
    storeToken(createToken({role: 'Admin'}), false);

    expect(service.isAdmin()).toBeTrue();
  });

  it('reads the .NET role claim and rejects non-admin roles', () => {
    const claim = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
    storeToken(createToken({[claim]: 'None'}), true);

    expect(service.isAdmin()).toBeFalse();

    storeToken(createToken({[claim]: ['Listener', 'Admin']}), false);
    expect(service.isAdmin()).toBeTrue();
  });

  function storeToken(token: string, isAdmin: boolean): void {
    localStorage.setItem('authToken', JSON.stringify({
      userId: 1,
      isAdmin,
      token,
      refreshToken: 'refresh',
      rememberMe: true
    }));
  }

  function createToken(claims: Record<string, unknown>): string {
    const header = encode({alg: 'none', typ: 'JWT'});
    const payload = encode({
      sub: '1',
      exp: Math.floor(Date.now() / 1000) + 3600,
      ...claims
    });
    return `${header}.${payload}.signature`;
  }

  function encode(value: Record<string, unknown>): string {
    return btoa(JSON.stringify(value))
      .replaceAll('+', '-')
      .replaceAll('/', '_')
      .replaceAll('=', '');
  }
});
