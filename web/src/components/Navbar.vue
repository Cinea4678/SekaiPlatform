<script setup lang="ts">
import { ChevronDown, LogOut, Menu, Repeat2, Search, User } from 'lucide-vue-next'
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuth } from '@/lib/auth'
import { formatTenantRole } from '@/lib/display'

defineProps<{
  onToggleSidebar: () => void
}>()

const route = useRoute()
const router = useRouter()
const { state, logout } = useAuth()
const searchQuery = ref('')
const accountDropdownOpen = ref(false)

const displayName = computed(() => state.user?.displayName || state.user?.qqId || '用户')
const currentTenantName = computed(() => state.currentTenant?.name || '未选择租户')
const canSwitchTenant = computed(() => state.tenants.length > 1)
const roleLabel = computed(() => formatTenantRole(state.currentTenant?.role))
const showGlobalSearch = computed(() => route.path !== '/search')

function handleSearch() {
  const keyword = searchQuery.value.trim()
  if (!keyword) {
    return
  }

  router.push({ path: '/search', query: { keyword } })
}

function toggleAccountDropdown() {
  accountDropdownOpen.value = !accountDropdownOpen.value
}

function closeAccountDropdown() {
  accountDropdownOpen.value = false
}

async function navigateToTenantSelect() {
  closeAccountDropdown()
  await router.push('/tenant/select')
}

async function handleLogout() {
  await logout()
  closeAccountDropdown()
  await router.replace('/login')
}
</script>

<template>
  <nav class="sticky top-0 z-40 border-b bg-card">
    <div class="px-4 lg:px-8">
      <div class="flex h-16 items-center justify-between gap-4">
        <div class="flex min-w-0 flex-1 items-center gap-4">
          <button
            class="rounded-md p-2 hover:bg-accent lg:hidden"
            aria-label="Toggle menu"
            @click="onToggleSidebar"
          >
            <Menu :size="20" />
          </button>

          <form v-if="showGlobalSearch" class="hidden max-w-xl flex-1 md:block" @submit.prevent="handleSearch">
            <div class="relative">
              <Search :size="18" class="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
              <input
                v-model="searchQuery"
                type="search"
                placeholder="搜索剧情原文或译文"
                class="w-full rounded-md border bg-background py-2 pl-10 pr-4 text-sm outline-none focus:ring-2 focus:ring-ring"
              >
            </div>
          </form>
        </div>

        <div class="relative">
          <button
            class="flex items-center gap-2 rounded-md px-3 py-2 hover:bg-accent"
            aria-label="Account menu"
            @click="toggleAccountDropdown"
          >
            <div class="flex h-8 w-8 items-center justify-center rounded-md bg-primary text-primary-foreground">
              <User :size="18" />
            </div>
            <span class="hidden text-left sm:block">
              <span class="block text-sm font-medium">{{ displayName }}</span>
              <span class="block max-w-44 truncate text-xs text-muted-foreground">{{ currentTenantName }}</span>
            </span>
            <ChevronDown :size="16" class="hidden sm:block" />
          </button>

          <div
            v-if="accountDropdownOpen"
            class="fixed inset-0 z-40"
            @click="closeAccountDropdown"
          />

          <transition
            enter-active-class="transition ease-out duration-100"
            enter-from-class="scale-95 opacity-0"
            enter-to-class="scale-100 opacity-100"
            leave-active-class="transition ease-in duration-75"
            leave-from-class="scale-100 opacity-100"
            leave-to-class="scale-95 opacity-0"
          >
            <div
              v-if="accountDropdownOpen"
              class="absolute right-0 z-50 mt-2 w-72 rounded-md border bg-card py-1 shadow-lg"
            >
              <div class="border-b px-4 py-3">
                <p class="text-sm font-medium">
                  {{ displayName }}
                </p>
                <p class="text-xs text-muted-foreground">
                  QQ：{{ state.user?.qqId || '未设置' }}
                </p>
                <p class="mt-2 text-xs text-muted-foreground">
                  {{ currentTenantName }} · {{ roleLabel }}
                </p>
              </div>

              <button
                class="flex w-full items-center gap-2 px-4 py-2 text-sm hover:bg-accent disabled:cursor-not-allowed disabled:opacity-50"
                :disabled="!canSwitchTenant"
                @click="navigateToTenantSelect"
              >
                <Repeat2 :size="16" />
                <span>切换租户</span>
              </button>

              <button
                class="flex w-full items-center gap-2 px-4 py-2 text-sm text-destructive hover:bg-accent"
                @click="handleLogout"
              >
                <LogOut :size="16" />
                <span>登出</span>
              </button>
            </div>
          </transition>
        </div>
      </div>

      <div v-if="showGlobalSearch" class="pb-3 md:hidden">
        <form @submit.prevent="handleSearch">
          <div class="relative">
            <Search :size="18" class="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <input
              v-model="searchQuery"
              type="search"
              placeholder="搜索剧情原文或译文"
              class="w-full rounded-md border bg-background py-2 pl-10 pr-4 text-sm outline-none focus:ring-2 focus:ring-ring"
            >
          </div>
        </form>
      </div>
    </div>
  </nav>
</template>
